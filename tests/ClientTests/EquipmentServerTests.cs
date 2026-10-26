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
        new(1015, "杖", 1, ItemCategory.Weapon, ItemSubCategory.Staff),
        new(1016, "枪", 1, ItemCategory.Weapon, ItemSubCategory.Spear),
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

    /// <summary>独立的公开装备快照。</summary>
    private RoleWorldEquipmentPb _equipment = new();

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
            if (message is RoleItemsChangedNotice notice) { _bag = notice.Bag; _equipment = notice.Equipment; }
            if (message is SelectRoleRt { Success: true } selected) { _bag = selected.Bag; _equipment = selected.State.Equipment; }
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
    private EquipItemRt Equip(long id, EquipmentSlotPb slot, long? revision = null, int? target = null, long? role = null, long? equipmentRevision = null)
    {
        _messages.Clear();
        var request = new EquipItem { RoleId = role ?? _roleId, InstanceId = id, Slot = slot,
            ExpectedBagRevision = revision ?? _bag.Revision, ExpectedEquipmentRevision = equipmentRevision ?? _equipment.Revision };
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
        Assert.Equal(replacement, Assert.Single(_equipment.Items).InstanceId);
        var before = _bag.Clone();
        Assert.False(Equip(0, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(before, _bag);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        Assert.True(Equip(0, EquipmentSlotPb.MainHand, target: sourceSlot).Success);
        _server.Flush();
        var persisted = _repo.Load(_roleId).Bag;
        Assert.Equal(4, persisted.NextInstanceId);
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(sword, Assert.Single(_equipment.Items).InstanceId);
        Assert.Equal(replacement, _bag.Items.Single(item => item.SlotIndex == sourceSlot).InstanceId);
        Assert.Equal(persisted.Revision, _bag.Revision);
    }

    /// <summary>role-equipment：一件主手拳刃保持单实例，连续穿戴替换主手，切角互不污染。</summary>
    [Fact]
    public void PairedWeaponIsOneMainHandInstanceAndCharacterIsolation()
    {
        var first = Grant(1017);
        Assert.True(Equip(first, EquipmentSlotPb.MainHand).Success);
        var second = Grant(1017);
        Assert.False(Equip(second, EquipmentSlotPb.OffHand).Success);
        Assert.True(Equip(second, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(second, Assert.Single(_equipment.Items).InstanceId);
        Assert.Equal(first, Assert.Single(_bag.Items).InstanceId);
        _server.Flush();
        var other = _repo.Create("另一角色", ServerRoleGender.Female, 1).RoleId;
        _server = CreateServer();
        Enter(other);
        Assert.Empty(_bag.Items);
        Assert.Empty(_equipment.Items);
        Enter(_roleId);
        Assert.Equal(second, Assert.Single(_equipment.Items).InstanceId);
    }

    /// <summary>role-equipment：飞刀仅主手，旧版本、错槽与角色请求保持原子拒绝。</summary>
    [Fact]
    public void RejectsStaleWrongSlotIdentityAndRepeatedRequests()
    {
        var knife = Grant(1018);
        var before = _bag.Clone();
        Assert.False(Equip(knife, EquipmentSlotPb.OffHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, revision: 0).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, role: _roleId + 1).Success);
        Assert.False(Equip(knife, (EquipmentSlotPb)999).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.Head).Success);
        Assert.False(Equip(999, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(before, _bag);
        Assert.True(Equip(knife, EquipmentSlotPb.MainHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, revision: before.Revision).Success);
        var shield = Grant(1014);
        Assert.False(Equip(shield, EquipmentSlotPb.MainHand).Success);
        var equipment = _equipment.Clone();
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(equipment, _equipment);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        Assert.Equal(shield, Assert.Single(_bag.Items).InstanceId);
    }

    /// <summary>role-equipment：剑盾切成对主手时自动收盾，满包失败不改变双版本或实例。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MainHandReplacementReturnsShieldAtomically(bool full)
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var shield = Grant(1014);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        var weapon = Grant(1018);
        if (full) Grant(1017);
        var beforeBag = _bag.Clone();
        var beforeEquipment = _equipment.Clone();
        Assert.Equal(!full, Equip(weapon, EquipmentSlotPb.MainHand).Success);
        if (full)
        {
            Assert.Equal(beforeBag, _bag);
            Assert.Equal(beforeEquipment, _equipment);
            Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        }
        else
        {
            Assert.Equal(weapon, Assert.Single(_equipment.Items).InstanceId);
            Assert.Equal(EquipmentSlotPb.MainHand, _equipment.Items[0].Slot);
            Assert.Equal(new[] { sword, shield }, _bag.Items.OrderBy(item => item.SlotIndex).Select(item => item.InstanceId));
            Assert.Equal(beforeBag.Revision + 1, _bag.Revision);
            Assert.Equal(beforeEquipment.Revision + 1, _equipment.Revision);
            Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
            _server.Flush();
            _server = CreateServer();
            Enter(_roleId);
            Assert.Equal(weapon, Assert.Single(_equipment.Items).InstanceId);
            Assert.Equal(2, _bag.Items.Count);
        }
    }

    /// <summary>role-equipment：全部双手分类自动收盾，反向交换也保持空间不足整次回滚。</summary>
    [Theory]
    [InlineData(1013, false)]
    [InlineData(1015, false)]
    [InlineData(1016, false)]
    [InlineData(1017, false)]
    [InlineData(1018, false)]
    [InlineData(1018, true)]
    public void ReverseExchangeEnforcesBothHands(int itemId, bool full)
    {
        var shield = Grant(1014);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Single(_equipment.Items);
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var weapon = Grant(itemId);
        if (full) Grant(1012);
        var beforeBag = _bag.Clone();
        var beforeEquipment = _equipment.Clone();
        var source = _bag.Items.Single(item => item.InstanceId == weapon).SlotIndex;
        Assert.Equal(!full, Equip(0, EquipmentSlotPb.MainHand, target: source).Success);
        if (full)
        {
            Assert.Equal(beforeBag, _bag);
            Assert.Equal(beforeEquipment, _equipment);
            Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
            return;
        }
        Assert.Equal(weapon, Assert.Single(_equipment.Items).InstanceId);
        Assert.Equal(sword, _bag.Items.Single(item => item.SlotIndex == source).InstanceId);
        Assert.Contains(_bag.Items, item => item.InstanceId == shield);
        Assert.Equal(beforeBag.Revision + 1, _bag.Revision);
        Assert.Equal(beforeEquipment.Revision + 1, _equipment.Revision);
        var accepted = _bag.Clone();
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(accepted, _bag);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
    }

    /// <summary>role-equipment：旧双拳刃、飞刀和带盾存档选角迁移，满包拒绝且原档不变。</summary>
    [Theory]
    [InlineData(0, 1018, false)]
    [InlineData(1017, 1017, false)]
    [InlineData(1013, 1014, false)]
    [InlineData(1017, 1018, true)]
    public void LegacyHandsMigrateWithoutLosingItems(int mainId, int offId, bool full)
    {
        var role = _repo.Load(_roleId);
        var equipment = new List<DataServerEquipment>();
        if (mainId != 0) equipment.Add(new(1, mainId, EquipmentSlot.MainHand));
        equipment.Add(new(2, offId, EquipmentSlot.OffHand));
        _repo.Save(role with { Bag = role.Bag with { Revision = 4, EquipmentRevision = 2, NextInstanceId = 5,
            Equipment = equipment, Items = full ? [new(3, 1012, 0, 1), new(4, 1012, 1, 1)] : [] } });
        var path = _repo.GetBagPath(_roleId);
        var original = File.ReadAllText(path);
        _server = CreateServer();
        if (full)
        {
            Send(new Login { Username = "demo", Password = "demo" });
            Send(new SelectRole { RoleId = _roleId });
            Assert.False(_messages.OfType<SelectRoleRt>().Last().Success);
            Assert.Contains("背包空间不足", _messages.OfType<SelectRoleRt>().Last().Error);
            _server.Flush();
            Assert.Equal(original, File.ReadAllText(path));
            return;
        }
        Enter(_roleId);
        Assert.Equal(mainId == 0 ? 2 : 1, Assert.Single(_equipment.Items).InstanceId);
        Assert.Equal(mainId == 0 ? offId : mainId, _equipment.Items[0].ItemId);
        Assert.Equal(EquipmentSlotPb.MainHand, _equipment.Items[0].Slot);
        Assert.Equal(mainId == 0 ? 0 : 1, _bag.Items.Count);
        if (mainId != 0) Assert.Equal(2, _bag.Items[0].InstanceId);
        Assert.Equal(5, _bag.Revision);
        Assert.Equal(3, _equipment.Revision);
        Assert.Equal(original, File.ReadAllText(path));
        _server.Flush();
        Assert.Equal(5, _repo.Load(_roleId).Bag.NextInstanceId);
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(5, _bag.Revision);
        Assert.Equal(3, _equipment.Revision);
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
        Assert.Equal(sword, Assert.Single(_equipment.Items).InstanceId);
        _server.Flush();
        var legacy = System.Text.Json.JsonSerializer.Deserialize<DataServerBag>("{\"Revision\":0,\"NextInstanceId\":1,\"Items\":[]}")!;
        legacy.Validate();
        Assert.Empty(legacy.Equipment);
    }

    /// <summary>role-equipment：背包整理不广播装备或改变装备版本，两个预期版本分别拒绝过期请求。</summary>
    [Fact]
    public void EquipmentVersionChangesOnlyOnWearAndRejectsEitherStaleVersion()
    {
        var sword = Grant(1012);
        Assert.Equal(0, _equipment.Revision);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(1, _equipment.Revision);
        var equipment = _equipment.Clone();
        var shield = Grant(1014);
        Send(new MoveItem { RoleId = _roleId, InstanceId = shield, TargetSlotIndex = 1, ExpectedRevision = _bag.Revision });
        Assert.Equal(equipment, _equipment);
        Assert.Empty(_messages.OfType<RoleEquipmentNotice>());
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand, equipmentRevision: 0).Success);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        Assert.Equal(equipment, _equipment);
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand, revision: _bag.Revision - 1).Success);
        Assert.Equal(equipment, _equipment);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        var notice = Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
        Assert.Equal(2, notice.Equipment.Revision);
        _server.Flush();
        Assert.Equal(notice.Equipment, MockServer.CreateEquipmentNotice(_repo.Load(_roleId)).Equipment);
        Enter(_roleId);
        Assert.Equal(notice.Equipment, _equipment);
    }

    /// <summary>mock-server：旧存档已有装备不能因版本迁移丢失，读取无写入，下次物品落地补存版本。</summary>
    [Fact]
    public void ExistingEquippedSaveMigratesVersionWithoutWritingOnRead()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        _server.Flush();
        var path = _repo.GetBagPath(_roleId);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        json["Bag"]!.AsObject().Remove("EquipmentRevision");
        var original = json.ToJsonString();
        File.WriteAllText(path, original);
        var previousRevision = _bag.Revision;
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal(previousRevision, _equipment.Revision);
        Assert.Equal(sword, Assert.Single(_equipment.Items).InstanceId);
        Grant(1014);
        Assert.Equal(previousRevision, _equipment.Revision);
        _server.Flush();
        Assert.Contains("EquipmentRevision", File.ReadAllText(path));
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(previousRevision, _equipment.Revision);
        Assert.Equal(sword, Assert.Single(_equipment.Items).InstanceId);
    }

    /// <summary>mock-server：完整角色主动提交若改变物品，也必须交付本人的原子组合快照。</summary>
    [Fact]
    public void FullRolePublicationAlsoCommitsInventoryAtomically()
    {
        var sword = Grant(1012);
        _server.Flush();
        var before = _repo.Load(_roleId);
        _messages.Clear();
        _server.PublishRoleState(_rules.Equip(before, sword, EquipmentSlot.MainHand, null));
        var items = Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
        var world = Assert.Single(_messages.OfType<RoleStateNotice>()).State;
        Assert.Equal(world.Equipment, items.Equipment);
        Assert.Empty(items.Bag.Items);
        Assert.Equal(sword, Assert.Single(items.Equipment.Items).InstanceId);
        Assert.Equal(before.Bag.Revision + 1, items.Bag.Revision);
        Assert.Equal(before.Bag.EquipmentRevision + 1, items.Equipment.Revision);
    }

    /// <summary>server-write-back：穿脱落地失败保留完整内存和待写，两侧在重试后共同恢复。</summary>
    [Fact]
    public void FailedEquipmentFlushRetriesTheWholeInventory()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var path = _repo.GetBagPath(_roleId);
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            Assert.Throws<AggregateException>(() => _server.Flush());
            Assert.Equal(sword, Assert.Single(_equipment.Items).InstanceId);
            Assert.Empty(_bag.Items);
        }
        finally
        {
            Directory.Delete(path);
            File.WriteAllText(path, original);
        }
        _server.Flush();
        var saved = _repo.Load(_roleId).Bag;
        Assert.Equal(_bag.Revision, saved.Revision);
        Assert.Equal(_equipment.Revision, saved.EquipmentRevision);
        Assert.Equal(sword, Assert.Single(saved.Equipment).InstanceId);
        Assert.Empty(saved.Items);
    }

    /// <summary>mock-server：损坏的背包不能在旧版本兼容时绕过存档诊断。</summary>
    [Fact]
    public void NullBagReportsItsSavePathDuringVersionMigration()
    {
        var path = _repo.GetBagPath(_roleId);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        json["Bag"] = null;
        File.WriteAllText(path, json.ToJsonString());
        var error = Assert.Throws<InvalidDataException>(() => _repo.Load(_roleId));
        Assert.Contains(path, error.Message);
        Assert.Contains("角色背包字段无效", error.Message);
    }

    /// <summary>清理本次隔离存档。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
