using Google.Protobuf;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 role-equipment 的真实字节请求、满包替换、失败和重启恢复。</summary>
public sealed class EquipmentServerTests : IDisposable
{
    /// <summary>独立存档目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-equipment-" + Guid.NewGuid());
    /// <summary>两个格子便于验收满包；静态规则含目标槽位分类。</summary>
    private readonly DataServerBagRules _rules = new(2, 1, [
        new(1012, "剑", 1, ItemCategory.Weapon, ItemSubCategory.Sword),
        new(1013, "双手剑", 1, ItemCategory.Weapon, ItemSubCategory.TwoHandedWeapon),
        new(1014, "盾", 1, ItemCategory.Armor, ItemSubCategory.Shield),
        new(1017, "拳刃", 1, ItemCategory.Weapon, ItemSubCategory.Katar),
        new(1018, "飞刀", 1, ItemCategory.Weapon, ItemSubCategory.ThrowingWeapon)]);
    /// <summary>读取实际领域文件的仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>独立解码的输出消息。</summary>
    private readonly List<IMessage> _messages = [];
    /// <summary>可重建的服务端。</summary>
    private MockServer _server;
    /// <summary>本次角色身份。</summary>
    private readonly long _roleId;
    /// <summary>最近权威快照。</summary>
    private BagPb _bag = new();

    /// <summary>创建已有角色并通过字节登录选角。</summary>
    public EquipmentServerTests()
    {
        _repo = new RepoServerRole(_directory);
        _roleId = _repo.Create("装备测试", ServerRoleGender.Male, 1).RoleId;
        _server = CreateServer();
        Enter(_roleId);
    }

    /// <summary>绑定真实消息输出并提取原子物品快照。</summary>
    private MockServer CreateServer()
    {
        var server = new MockServer(_directory, _rules);
        server.ResetSession(1);
        server.Outgoing += bytes =>
        {
            var message = MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false);
            _messages.Add(message);
            if (message is BagChangedNotice notice) _bag = notice.Bag;
            if (message is SelectRoleRt { Success: true } selected) _bag = selected.Bag;
        };
        return server;
    }

    /// <summary>通过已注册方向发送独立信封。</summary>
    private void Send(IMessage message) => _server.Receive(MessageCodec.Encode(message, 1, true));

    /// <summary>登录并选择已有角色。</summary>
    private void Enter(long id)
    {
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = id });
        Assert.True(_messages.OfType<SelectRoleRt>().Last().Success);
        _messages.Clear();
    }

    /// <summary>用 GM 发放一件并返回稳定编号。</summary>
    private long Grant(int id)
    {
        Send(new Gm { RoleId = _roleId, Command = $"item {id}" });
        Assert.True(_messages.OfType<GmRt>().Last().Success);
        return _bag.Items.Last(item => item.ItemId == id).InstanceId;
    }

    /// <summary>提交穿脱，可显式构造非法身份、旧版本和卸下目标。</summary>
    private EquipItemRt Equip(long id, EquipmentSlotPb slot, long? revision = null, int? target = null, long? role = null)
    {
        _messages.Clear();
        var request = new EquipItem { RoleId = role ?? _roleId, InstanceId = id, Slot = slot,
            ExpectedRevision = revision ?? _bag.Revision };
        if (target.HasValue) request.TargetBagSlot = target.Value;
        Send(request);
        return Assert.Single(_messages.OfType<EquipItemRt>());
    }

    /// <summary>满包替换回来源格；卸下失败不丢物，目标交换与重启保持实例。</summary>
    [Fact]
    public void SwapFullBagUnequipAndRestartPreserveInstances()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var replacement = Grant(1013);
        Grant(1014);
        var sourceSlot = _bag.Items.Single(item => item.InstanceId == replacement).SlotIndex;
        Assert.True(Equip(replacement, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(sword, _bag.Items.Single(item => item.SlotIndex == sourceSlot).InstanceId);
        Assert.Equal(replacement, Assert.Single(_bag.Equipment).InstanceId);
        var before = _bag.Clone();
        Assert.False(Equip(0, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(before, _bag);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.True(Equip(0, EquipmentSlotPb.MainHand, target: sourceSlot).Success);
        _server.Flush();
        var persisted = _repo.Load(_roleId).Bag;
        Assert.Equal(4, persisted.NextInstanceId);
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(sword, Assert.Single(_bag.Equipment).InstanceId);
        Assert.Equal(replacement, _bag.Items.Single(item => item.SlotIndex == sourceSlot).InstanceId);
        Assert.Equal(persisted.Revision, _bag.Revision);
    }

    /// <summary>双拳刃可独立穿脱，切到新角色时不带入原角色装备。</summary>
    [Fact]
    public void IndependentKatarsAndCharacterIsolation()
    {
        var first = Grant(1017);
        Assert.True(Equip(first, EquipmentSlotPb.MainHand).Success);
        var second = Grant(1017);
        Assert.True(Equip(second, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(2, _bag.Equipment.Count);
        Assert.True(Equip(0, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(second, Assert.Single(_bag.Equipment).InstanceId);
        Assert.Equal(first, Assert.Single(_bag.Items).InstanceId);
        _server.Flush();
        var other = _repo.Create("另一角色", ServerRoleGender.Female, 1).RoleId;
        _server = CreateServer();
        Enter(other);
        Assert.Empty(_bag.Items);
        Assert.Empty(_bag.Equipment);
        Enter(_roleId);
        Assert.Equal(second, Assert.Single(_bag.Equipment).InstanceId);
    }

    /// <summary>错误槽位、旧版本和非法身份不修改状态，飞刀盾牌只接受副手。</summary>
    [Fact]
    public void RejectsStaleWrongSlotIdentityAndRepeatedRequests()
    {
        var knife = Grant(1018);
        var before = _bag.Clone();
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.OffHand, revision: 0).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.OffHand, role: _roleId + 1).Success);
        Assert.False(Equip(knife, (EquipmentSlotPb)999).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.Head).Success);
        Assert.False(Equip(999, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(before, _bag);
        Assert.True(Equip(knife, EquipmentSlotPb.OffHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.OffHand, revision: before.Revision).Success);
        var shield = Grant(1014);
        Assert.False(Equip(shield, EquipmentSlotPb.MainHand).Success);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(knife, Assert.Single(_bag.Items).InstanceId);
    }

    /// <summary>GM、背包移动不覆盖已穿装备，旧存档默认空装备。</summary>
    [Fact]
    public void GrantsAndMovesKeepEquipmentAndLegacyFilesStayEmpty()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var shield = Grant(1014);
        Send(new MoveItem { RoleId = _roleId, InstanceId = shield, TargetSlotIndex = 1, ExpectedRevision = _bag.Revision });
        Assert.True(_messages.OfType<MoveItemRt>().Last().Success);
        Assert.Equal(sword, Assert.Single(_bag.Equipment).InstanceId);
        _server.Flush();
        var legacy = System.Text.Json.JsonSerializer.Deserialize<DataServerBag>("{\"Revision\":0,\"NextInstanceId\":1,\"Items\":[]}")!;
        legacy.Validate();
        Assert.Empty(legacy.Equipment);
    }

    /// <summary>清理本次隔离存档。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
