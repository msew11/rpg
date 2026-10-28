using System.Linq;
using Google.Protobuf;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;
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
        var cache = new RepoServerRole(_directory, deferWrites: true);
        cache.SaveBag(role.WithItems([new(1, 1012, EquipmentSlot.MainHand)], 2));
        Assert.Equal(new[] { 1012 }, Assert.Single(cache.ListSummaries()).EquippedItems);
        using (var locked = new FileStream(disk.GetSummaryPath(role.Role.Value.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => cache.Flush());
        Assert.Empty(disk.Load(role.Role.Value.RoleId).EquippedItems);
        Assert.Empty(Assert.Single(disk.ListSummaries()).EquippedItems);
        Assert.Equal(new[] { 1012 }, Assert.Single(cache.ListSummaries()).EquippedItems);
        cache.Flush();
        var loaded = disk.Load(role.Role.Value.RoleId);
        Assert.Equal(1, Assert.Single(loaded.EquippedItems).InstanceId);
        Assert.Equal(new[] { 1012 }, Assert.Single(new RepoServerRole(_directory).ListSummaries()).EquippedItems);
        var entities = loaded;
        Assert.IsType<RoleEntity>(entities.Role.Value);
        Assert.Equal(entities.Item.Items.Keys, entities.Equipment.Items.Keys);
        Assert.True(entities.Item.Items[1].Equipped);
        Assert.Equal(0, entities.Item.Items[1].SlotIndex);
    }

    /// <summary>mock-server：平级实体表重组保留完整角色、穿戴及编号，会话序号与实体表分离。</summary>
    [Fact]
    public void PeerTablesPreserveRoleStateAndInstanceNumbers()
    {
        var original = new RepoServerRole(_directory).Create("平级缓存", ServerRoleGender.Female, 2);
        var role = original.WithRole(original.Role.Value with { BagExtraRows = 1 })
            .WithItems([new(3, 1014, 2, 1), new(10, 1012, EquipmentSlot.MainHand)], 11);
        var restored = new RoleEntityTables(role.Role, role.Item, role.Equipment);
        Assert.Equal(role, restored);
        Assert.Same(role.Role, restored.Role);
        Assert.Same(role.Item, restored.Item);
        Assert.Same(role.Equipment, restored.Equipment);
        Assert.Equal(11, restored.Role.Value.NextInstanceId);
        Assert.Equal(EquipmentSlot.MainHand, restored.Item.Items[10].EquipmentSlot);

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
