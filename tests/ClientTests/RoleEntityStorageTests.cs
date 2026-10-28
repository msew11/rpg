using System.Linq;
using Google.Protobuf;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Context;
using Rpg.Tests;
using Rpg.Services;

namespace ClientTests;

/// <summary>验证 mock-server、client-network 与 role-management 的实体缓存、摘要懒加载及信封版本。</summary>
public sealed class RoleEntityStorageTests : IDisposable
{
    /// <summary>独占文件根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-entity-" + Guid.NewGuid());
    /// <summary>测试只需一件主手武器。</summary>
    private readonly BagRules _rules = new(8, 5,
        [RoleTestData.Item(1012, "剑", 1, ItemCategory.Weapon, ItemSubCategory.Sword)]);

    /// <summary>mock-server：登录只读摘要，任一完整实体表锁定时仍列出装备并能选另一个角色。</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("item")]
    [InlineData("equipment")]
    public void LoginAndOtherSelectionDoNotReadUnselectedTables(string domain)
    {
        var repo = new RepoServerRole(_directory);
        var first = repo.Create("摘要角色", ServerRoleGender.Male, 1);
        repo.SaveBag(first.WithItems([new(1, 1012, EquipmentSlot.MainHand)], 2));
        var second = repo.Create("可选角色", ServerRoleGender.Female, 2);
        var server = new MockServer(_directory, _rules);
        server.ResetSession(1);
        var messages = new List<IMessage>();
        server.Outgoing += packet => messages.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        using var locked = new FileStream(Path.Combine(_directory, $"{first.Role.Value.RoleId}.{domain}.json"),
            FileMode.Open, FileAccess.Read, FileShare.None);
        server.Receive(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        var login = Assert.Single(messages.OfType<LoginRt>());
        Assert.True(login.Success, login.Error);
        Assert.Equal(new[] { 1012 }, login.Roles.Single(role => role.RoleId == first.Role.Value.RoleId).EquippedItems);
        server.Receive(MessageCodec.Encode(new SelectRole { RoleId = first.Role.Value.RoleId }, 1, true));
        Assert.False(messages.OfType<SelectRoleRt>().Last().Success);
        server.Receive(MessageCodec.Encode(new SelectRole { RoleId = second.Role.Value.RoleId }, 1, true));
        Assert.True(messages.OfType<SelectRoleRt>().Last().Success);
    }

    /// <summary>mock-server：摘要必须与穿戴实体共同落地，失败回滚磁盘但缓存保留最新列表供重试。</summary>
    [Fact]
    public void EquipmentSummaryCommitsAndRecoversWithItemTables()
    {
        var disk = new RepoServerRole(_directory);
        var role = disk.Create("摘要事务", ServerRoleGender.Male, 1);
        using var server = new MockServer(_directory, _rules);
        server.ResetSession(1);
        var replies = new List<IMessage>();
        server.Outgoing += packet => replies.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        void Send(IMessage request) => server.Receive(MessageCodec.Encode(request, 1, true));
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = role.Role.Value.RoleId });
        server.PublishRoleState(role.WithItems([new(1, 1012, EquipmentSlot.MainHand)], 2));
        server.ResetSession(1);
        Send(new Login { Username = "demo", Password = "demo" });
        Assert.Equal(new[] { 1012 }, Assert.Single(replies.OfType<LoginRt>().Last().Roles).EquippedItems);
        using (var locked = new FileStream(disk.GetSummaryPath(role.Role.Value.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => server.Flush());
        Assert.Empty(disk.Load(role.Role.Value.RoleId).EquippedItems);
        Assert.Empty(Assert.Single(disk.ListSummaries()).EquippedItems);
        Send(new Login { Username = "demo", Password = "demo" });
        Assert.Equal(new[] { 1012 }, Assert.Single(replies.OfType<LoginRt>().Last().Roles).EquippedItems);
        server.Flush();
        var loaded = disk.Load(role.Role.Value.RoleId);
        Assert.Equal(1, Assert.Single(loaded.EquippedItems).InstanceId);
        Assert.Equal(new[] { 1012 }, Assert.Single(new RepoServerRole(_directory).ListSummaries()).EquippedItems);
        var entities = loaded;
        Assert.IsType<RoleEntity>(entities.Role.Value);
        Assert.Equal(entities.Item.Items.Keys, entities.Equipment.Items.Keys);
        Assert.True(entities.Item.Items[1].Equipped);
        Assert.Equal(0, entities.Item.Items[1].SlotIndex);
    }

    /// <summary>openspec/specs/mock-server：角色缓存直接持有三份实体缓存并保留穿戴、扩容及实例编号。</summary>
    [Fact]
    public void RoleCachePreservesPeerEntitiesAndInstanceNumbers()
    {
        var original = new RepoServerRole(_directory).Create("平级缓存", ServerRoleGender.Female, 2);
        var role = original.WithRole(original.Role.Value with { BagExtraRows = 1 })
            .WithItems([new(3, 1014, 2, 1), new(10, 1012, EquipmentSlot.MainHand)], 11);
        var restored = new ServerRoleCache(role.Role, role.Item, role.Equipment);
        Assert.Equal(role, restored);
        Assert.Same(role.Role, restored.Role);
        Assert.Same(role.Item, restored.Item);
        Assert.Same(role.Equipment, restored.Equipment);
        Assert.Equal(11, restored.Role.Value.NextInstanceId);
        Assert.Equal(EquipmentSlot.MainHand, restored.Item.Items[10].EquipmentSlot);
    }

    /// <summary>openspec/specs/server-actors：工作副本共享只读实体，但会话及其可写集合与来源隔离。</summary>
    [Fact]
    public void RoleWorkCopyIsolatesSessionAndEntityCommits()
    {
        var original = new RepoServerRole(_directory).Create("副本隔离", ServerRoleGender.Male, 1);
        original.Dirty = 1;
        original.Session.Authenticated = true;
        original.Session.RoleId = original.Role.Value.RoleId;
        original.Session.ItemsRevision = 7;
        original.Session.Moves.Add(new PendingMove(1, 0.45));
        original.Session.Pickup = new PickupItem { RoleId = original.Role.Value.RoleId, MoveActionId = 1 };
        var copy = original.Copy();
        Assert.Same(original.Role, copy.Role);
        Assert.Same(original.Item, copy.Item);
        Assert.Same(original.Equipment, copy.Equipment);
        Assert.NotSame(original.Session, copy.Session);
        Assert.NotSame(original.Session.Pickup, copy.Session.Pickup);
        Assert.Equal(1, copy.Dirty);
        Assert.Equal(7, copy.Session.ItemsRevision);

        var context = new RoleContext(copy, new DataServerRules(_rules));
        context.Commit(copy.WithItems([new(1, 1012, 0, 1)], 2), 2);
        copy.Session.ItemsRevision = 9;
        copy.Session.Moves.Clear();
        copy.Session.Pickup!.MoveActionId = 2;
        Assert.Single(copy.BagItems);
        Assert.Equal(3, copy.Dirty);
        Assert.Empty(original.BagItems);
        Assert.Equal(1, original.Role.Value.NextInstanceId);
        Assert.Equal(1, original.Dirty);
        Assert.Equal(7, original.Session.ItemsRevision);
        Assert.Single(original.Session.Moves);
        Assert.Equal(1, original.Session.Pickup.MoveActionId);
    }

    /// <summary>openspec/specs/mock-server、server-write-back：领域提交仅接纳对应实体字段，保留会话和既有待写标记。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EntityCommitPreservesSessionAndFiltersDomains(int domain)
    {
        var cache = new RepoServerRole(_directory).Create("领域提交", ServerRoleGender.Male, 1);
        var before = cache.Role.Value;
        var items = cache.Item;
        cache.Dirty = domain == 1 ? 2 : 1;
        cache.Session.RoleId = before.RoleId;
        cache.Session.AcknowledgedAction = 5;
        cache.Session.Moves.Add(new PendingMove(6, 0.45));
        var session = cache.Session;
        var context = new RoleContext(cache, new DataServerRules(_rules));
        var candidate = cache.WithRole(before with { Health = 77, BagExtraRows = 2 })
            .WithItems([new(1, 1012, 0, 1)], 2);
        context.Commit(candidate, domain);
        Assert.Same(session, cache.Session);
        Assert.Equal(5, session.AcknowledgedAction);
        Assert.Single(session.Moves);
        Assert.Equal(domain == 2 ? before.Health : 77, cache.Role.Value.Health);
        Assert.Equal(domain == 1 ? before.BagExtraRows : 2, cache.Role.Value.BagExtraRows);
        Assert.Equal(domain == 1 ? before.NextInstanceId : 2, cache.Role.Value.NextInstanceId);
        Assert.Equal(domain == 1 ? 0 : 1, cache.BagItems.Count);
        if (domain == 1) Assert.Same(items, cache.Item);
        Assert.Equal(3, cache.Dirty);
        cache.Dirty = 0;
        context.Commit(cache.Copy(), domain);
        Assert.Equal(0, cache.Dirty);
    }

    /// <summary>openspec/specs/mock-server：编号越界、重复背包格或装备槽的候选失败时不改变当前缓存。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidEntityCandidatesLeaveCurrentCacheUntouched(int invalidCase)
    {
        var cache = new RepoServerRole(_directory).Create("跨表校验", ServerRoleGender.Male, 1);
        var role = cache.Role;
        var item = cache.Item;
        var equipment = cache.Equipment;
        ItemEntity[] invalid = invalidCase == 2
            ? [new(1, 1012, EquipmentSlot.MainHand), new(2, 1012, EquipmentSlot.MainHand)]
            : [new(1, 1012, 0, 1), new(2, 1012, 0, 1)];
        if (invalidCase == 0) invalid = [new(3, 1012, 0, 1)];
        Assert.Throws<InvalidDataException>(() => cache.WithItems(invalid, 3));
        Assert.Same(role, cache.Role);
        Assert.Same(item, cache.Item);
        Assert.Same(equipment, cache.Equipment);
        Assert.Equal(0, cache.Dirty);
    }

    /// <summary>mock-server：多例缓存以 InstanceId 拒绝重复、悬空关联和缺失穿戴扩展。</summary>
    [Fact]
    public void EntityCachesEnforceSharedPrimaryKeys()
    {
        var item = new ItemEntity { InstanceId = 5, ItemId = 1012, Count = 1,
            Equipped = true, EquipmentSlot = EquipmentSlot.MainHand };
        Assert.Throws<InvalidDataException>(() => new ItemEntityCache([item, item]));
        Assert.Throws<InvalidDataException>(() => new ItemEntityCache([item with { SlotIndex = 1 }]));
        var items = new ItemEntityCache([item]);
        Assert.Throws<InvalidDataException>(() => new EquipmentEntityCache([], items));
        Assert.Throws<InvalidDataException>(() => new EquipmentEntityCache([new() { InstanceId = 6 }], items));
        Assert.Throws<InvalidDataException>(() => new EquipmentEntityCache(
            [new() { InstanceId = 5 }, new() { InstanceId = 5 }], items));
        Assert.Equal(5, Assert.Single(new EquipmentEntityCache([new() { InstanceId = 5 }], items).Items).Key);
    }

    /// <summary>client-network：双向每条消息递增信封版本，换会话不断号，重建发送端从一开始。</summary>
    [Fact]
    public void RevisionsBelongToEnvelopesAndRestartWithoutPersistence()
    {
        var repo = new RepoServerRole(_directory);
        var role = repo.Create("消息版本", ServerRoleGender.Male, 1);
        var server = new MockServer(_directory, _rules);
        server.ResetSession(1);
        var envelopes = new List<EnvelopePb>();
        server.Outgoing += packet => envelopes.Add(MessageCodec.ReadEnvelope(packet));
        server.Receive(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        server.Receive(MessageCodec.Encode(new SelectRole { RoleId = role.Role.Value.RoleId }, 1, true));
        server.Receive(MessageCodec.Encode(new Gm { RoleId = role.Role.Value.RoleId, Command = "item 1012" }, 1, true));
        var inventory = Assert.IsType<RoleItemsChangedNotice>(envelopes.Select(e => MessageCodec.ReadPayload(e, false))
            .Single(message => message is RoleItemsChangedNotice));
        Assert.Equal(inventory.GetRevision(), inventory.Bag.GetRevision());
        Assert.Equal(inventory.GetRevision(), inventory.Equipment.GetRevision());
        Assert.NotNull(Assert.Single(inventory.Bag.Items).Equipment);
        server.ResetSession(2);
        server.Receive(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 2, true));
        Assert.Equal(Enumerable.Range(1, envelopes.Count).Select(n => (long)n), envelopes.Select(e => e.Revision));
        server.Flush();
        foreach (var file in Directory.GetFiles(_directory, $"{role.Role.Value.RoleId}.*.json"))
            Assert.DoesNotContain("revision", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
        var restarted = new MockServer(_directory, _rules);
        restarted.ResetSession(1);
        EnvelopePb? first = null;
        restarted.Outgoing += packet => first = MessageCodec.ReadEnvelope(packet);
        restarted.Receive(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        Assert.Equal(1, first!.Revision);

        using var events = new EventBusService();
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, events, new LogService(_ => { }));
        net.Send(new Login()); net.Send(new Login()); net.Tick(0);
        net.ResetSession(); net.Send(new Login()); net.Tick(0);
        Assert.Equal(new long[] { 1, 2, 3 }, transport.Sent.Select(packet => MessageCodec.ReadEnvelope(packet).Revision));
        Assert.Null(ItemPb.Descriptor.FindFieldByName("equipment_slot"));
        Assert.NotNull(MapObjectPb.Descriptor.FindFieldByName("revision"));
    }

    /// <summary>ground-item-drop：不支持的事务版本只报错，不改角色或删除原记录。</summary>
    [Fact]
    public void UnsupportedTransferManifestDoesNotTouchRoles()
    {
        var repo = new RepoServerRole(_directory);
        var role = repo.Create("事务版本", ServerRoleGender.Male, 1);
        var path = repo.GetRolePath(role.Role.Value.RoleId);
        var original = File.ReadAllText(path);
        var transfer = new RepoServerItemTransfer(_directory);
        var pending = System.Text.Json.JsonSerializer.Serialize(new
        {
            FormatVersion = 1, MapId = 1, Roles = new[] { role.Role.Value.RoleId },
            Originals = new Dictionary<string, string?>
            {
                [$"{role.Role.Value.RoleId}.role.json"] = original,
                [$"{role.Role.Value.RoleId}.bag.json"] = null,
                [$"{role.Role.Value.RoleId}.pending.json"] = null,
                [Path.Combine("maps", "1", "manifest.json")] = null
            }
        });
        File.WriteAllText(transfer.PendingPath, pending);
        Assert.Throws<IOException>(() => transfer.Recover());
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal(pending, File.ReadAllText(transfer.PendingPath));
    }
    /// <summary>清理仅属于本次验收的文件。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
