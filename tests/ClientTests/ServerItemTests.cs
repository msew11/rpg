using System.Collections.Immutable;
using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Handler;
using Rpg.Server.Modules.Role.Storage;
using Codec = Rpg.Server.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>验证 server-items 最新状态规则、原子失败、多人公开装备和落盘恢复。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerItemTests : IDisposable
{
    /// <summary>本例独占存档。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-server-items-" + Guid.NewGuid());
    /// <summary>请求序号。</summary>
    private ulong _request;

    /// <summary>合并以当前格子为准，满包及双手冲突失败不消耗编号或改变原快照。</summary>
    [Fact]
    public void RulesUseLatestStateAndRejectWholeCandidate()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        var rules = new ItemRules(2, 1, config.Item.Items.Values.Append(new Rpg.Config.Data.ItemCfg { Id = 2000, Name = "堆叠验收", MaxStack = 99, Category = Rpg.Const.ItemCategory.Weapon, SubCategory = Rpg.Const.ItemSubCategory.Sword, RelatedId = 1012 }));
        var empty = new DataRoleSnapshot(new DataRole { RoleId = 1, NextInstanceId = 1 }, [], []);
        var added = rules.Add(empty, 2000, 3); var original = added.Items.Single();
        var moved = rules.Move(added, original.InstanceId, 1);
        Assert.Equal(1, moved.Items.Single().SlotIndex); Assert.Equal(0, added.Items.Single().SlotIndex);
        var partial = rules.Remove(moved, original.InstanceId, 2);
        Assert.Equal(1, partial.Items.Single().Count); Assert.Equal(moved.Role.NextInstanceId, partial.Role.NextInstanceId);
        Assert.Empty(rules.Remove(partial, original.InstanceId, 1).Items);
        var stacks = rules.Add(empty, 2000, 100);
        var merged = rules.Move(stacks, 1, 1);
        Assert.Equal(99, merged.Items.Single(item => item.InstanceId == 2).Count);
        Assert.Equal(1, merged.Items.Single(item => item.InstanceId == 1).Count);
        var filled = rules.Add(moved, 1012, 1);
        Assert.Throws<InvalidOperationException>(() => rules.Add(filled, 1012, 1));
        Assert.Equal(3, filled.Role.NextInstanceId); Assert.Equal(2, filled.Items.Length);
        var equipped = rules.Equip(filled, 2, Rpg.Const.EquipmentSlot.MainHand);
        Assert.True(equipped.Items.Single(item => item.InstanceId == 2).Equipped);
        var replaced = rules.Add(equipped, 1013, 1);
        var swapped = rules.Equip(replaced, 3, Rpg.Const.EquipmentSlot.MainHand);
        Assert.Equal(0, swapped.Items.Single(item => item.InstanceId == 2).SlotIndex);
        Assert.True(swapped.Items.Single(item => item.InstanceId == 3).Equipped);
        Assert.DoesNotContain(EquipItem.Descriptor.Fields.InDeclarationOrder(), field => field.Name.Contains("revision"));
    }

    /// <summary>所有开发账号可发放；本人通知先于回复，静止换装进入公开增量并立即更新摘要，重启恢复。</summary>
    [Fact]
    public async Task AccountsReceiveOrderedItemsAndEquipmentSurvivesRestart()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        long savedId = 0;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var observer = server.OpenConnection();
            foreach (var account in new[] { "admin", "demo1", "demo2" })
            {
                var id = account == "admin" ? observer : server.OpenConnection();
                await Exchange(server, id, new Login { Username = account, Password = "123" });
                var role = Assert.Single(Assert.IsType<CreateRoleRt>((await Exchange(server, id,
                    new CreateRole { Name = account, Gender = RoleGenderPb.Male, HeadId = 1 })).Last()).Roles).RoleId;
                await Exchange(server, id, new EnterWorld { RoleId = role });
                var granted = await Exchange(server, id, new Gm { Command = "item 1012" });
                var notice = Assert.IsType<RoleItemsChangedNotice>(granted[0]);
                Assert.IsType<GmRt>(granted[1]);
                var equipped = await Exchange(server, id, new EquipItem { InstanceId = notice.Bag.Items.Single().InstanceId, Slot = EquipmentSlotPb.MainHand });
                Assert.True(Assert.IsType<RoleItemsChangedNotice>(equipped[0]).Bag.Items.Single().Equipped);
                Assert.IsType<EquipItemRt>(equipped[1]);
                if (account == "demo2") savedId = role;
                if (account != "admin")
                {
                    await Task.Delay(110); await server.Server!.AdvanceAsync();
                    var frames = ReadAll(server, observer).OfType<WorldUpdateNotice>().ToArray();
                    Assert.Contains(frames, n => n.Frame.Delta?.EnteredRoles.Any(r => r.RoleId == role && r.Equipment.EquippedItems.Contains(1012)) == true ||
                        n.Frame.Delta?.EquippedRoles.Any(r => r.RoleId == role && r.Equipment.EquippedItems.Contains(1012)) == true);
                }
                var login = Assert.IsType<LoginRt>((await Exchange(server, id, new Login { Username = account, Password = "123" })).Last());
                Assert.Equal(1012, Assert.Single(login.Roles.Single(r => r.RoleId == role).EquippedItems));
                if (account == "admin") await Exchange(server, id, new EnterWorld { RoleId = role });
            }
        }
        var stored = await new RepoRole(Path.GetFullPath(_root)).LoadAsync(savedId);
        Assert.True(Assert.Single(stored.Items).Equipped); Assert.Single(stored.Equipments);
    }
    /// <summary>保留推送以检验顺序，只在请求对应回复到达时返回。</summary>
    private async Task<List<IMessage>> Exchange(MockServerService server, DataConnectionId id, IMessage request)
    {
        ReadAll(server, id); var number = ++_request;
        await server.ReceiveAsync(id, Codec.Encode(request, server.Session(id).Generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        var result = new List<IMessage>();
        while (server.TryReadOutgoing(id, out var bytes))
        { var envelope = Codec.ReadEnvelope(bytes); result.Add(Codec.ReadPayload(envelope, false)); if (envelope.RequestId == number) return result; }
        throw new InvalidOperationException("请求未回复");
    }
    /// <summary>排空已有推送。</summary>
    private static List<IMessage> ReadAll(MockServerService server, DataConnectionId id)
    { var result = new List<IMessage>(); while (server.TryReadOutgoing(id, out var bytes)) result.Add(Codec.ReadPayload(Codec.ReadEnvelope(bytes), false)); return result; }
    /// <summary>清理独占文件。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
