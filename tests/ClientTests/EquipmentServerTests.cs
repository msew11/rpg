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

    /// <summary>只读取未穿实例以验收背包位置。</summary>
    private IReadOnlyList<ItemPb> BagItems => _bag.Items.Where(item => !item.Equipped).ToArray();
    /// <summary>私有实例推导操作槽位，公开协议不含实例编号。</summary>
    private IReadOnlyList<Rpg.Session.Data.EquippedItemData> EquippedItems => _bag.Items.Where(item => item.Equipped)
        .Select(item => new Rpg.Session.Data.EquippedItemData(item.InstanceId, item.ItemId,
            Rpg.Utils.EquipmentSlotUtil.TryGetSlot(_rules.Items[item.ItemId].Category,
                _rules.Items[item.ItemId].SubCategory, out var slot) ? slot : default)).ToArray();

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
        return BagItems.Last(item => item.ItemId == id).InstanceId;
    }

    /// <summary>提交穿脱，可显式构造非法身份、旧版本和卸下目标。</summary>
    private EquipItemRt Equip(long id, EquipmentSlotPb slot, long? revision = null, int? target = null, long? role = null, long? equipmentRevision = null)
    {
        _messages.Clear();
        var request = new EquipItem { RoleId = role ?? _roleId, InstanceId = id, Slot = slot,
            ExpectedBagRevision = revision ?? _bag.GetRevision(), ExpectedEquipmentRevision = equipmentRevision ?? _equipment.GetRevision() };
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
        var sourceSlot = BagItems.Single(item => item.InstanceId == replacement).SlotIndex;
        Assert.True(Equip(replacement, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(sword, BagItems.Single(item => item.SlotIndex == sourceSlot).InstanceId);
        Assert.Equal(replacement, Assert.Single(EquippedItems).InstanceId);
        var before = _bag.Clone().WithRevision(_bag.GetRevision());
        Assert.False(Equip(0, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(before, _bag);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        Assert.True(Equip(0, EquipmentSlotPb.MainHand, target: sourceSlot).Success);
        _server.Flush();
        var persisted = _repo.Load(_roleId).Bag;
        Assert.Equal(4, persisted.NextInstanceId);
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(sword, Assert.Single(EquippedItems).InstanceId);
        Assert.Equal(replacement, BagItems.Single(item => item.SlotIndex == sourceSlot).InstanceId);
        Assert.Equal(0, persisted.Revision);
        Assert.True(_bag.GetRevision() > 0);
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
        Assert.Equal(second, Assert.Single(EquippedItems).InstanceId);
        Assert.Equal(first, Assert.Single(BagItems).InstanceId);
        _server.Flush();
        var other = _repo.Create("另一角色", ServerRoleGender.Female, 1).RoleId;
        _server = CreateServer();
        Enter(other);
        Assert.Empty(BagItems);
        Assert.Empty(EquippedItems);
        Enter(_roleId);
        Assert.Equal(second, Assert.Single(EquippedItems).InstanceId);
    }

    /// <summary>role-equipment：飞刀仅主手，旧版本、错槽与角色请求保持原子拒绝。</summary>
    [Fact]
    public void RejectsStaleWrongSlotIdentityAndRepeatedRequests()
    {
        var knife = Grant(1018);
        var before = _bag.Clone().WithRevision(_bag.GetRevision());
        Assert.False(Equip(knife, EquipmentSlotPb.OffHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, revision: 0).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, role: _roleId + 1).Success);
        Assert.False(Equip(knife, (EquipmentSlotPb)999).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.Head).Success);
        Assert.False(Equip(999, EquipmentSlotPb.MainHand).Success);
        Assert.Equal(before, _bag);
        Assert.True(Equip(knife, EquipmentSlotPb.MainHand).Success);
        Assert.False(Equip(knife, EquipmentSlotPb.MainHand, revision: before.GetRevision()).Success);
        var shield = Grant(1014);
        Assert.False(Equip(shield, EquipmentSlotPb.MainHand).Success);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(shield, Assert.Single(EquippedItems).InstanceId);
        Assert.Equal(knife, Assert.Single(BagItems).InstanceId);
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
        var beforeBag = _bag.Clone().WithRevision(_bag.GetRevision());
        var beforeEquipment = _equipment.Clone().WithRevision(_equipment.GetRevision());
        Assert.Equal(!full, Equip(weapon, EquipmentSlotPb.MainHand).Success);
        if (full)
        {
            Assert.Equal(beforeBag, _bag);
            Assert.Equal(beforeEquipment, _equipment);
            Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        }
        else
        {
            Assert.Equal(weapon, Assert.Single(EquippedItems).InstanceId);
            Assert.Equal(EquipmentSlot.MainHand, EquippedItems[0].Slot);
            Assert.Equal(new[] { sword, shield }, BagItems.OrderBy(item => item.SlotIndex).Select(item => item.InstanceId));
            Assert.True(_bag.GetRevision() > beforeBag.GetRevision());
            Assert.True(_equipment.GetRevision() > beforeEquipment.GetRevision());
        Assert.Equal(_bag.GetRevision(), _equipment.GetRevision());
            Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
            _server.Flush();
            _server = CreateServer();
            Enter(_roleId);
            Assert.Equal(weapon, Assert.Single(EquippedItems).InstanceId);
            Assert.Equal(2, BagItems.Count);
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
        Assert.Single(EquippedItems);
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var weapon = Grant(itemId);
        if (full) Grant(1012);
        var beforeBag = _bag.Clone().WithRevision(_bag.GetRevision());
        var beforeEquipment = _equipment.Clone().WithRevision(_equipment.GetRevision());
        var source = BagItems.Single(item => item.InstanceId == weapon).SlotIndex;
        Assert.Equal(!full, Equip(0, EquipmentSlotPb.MainHand, target: source).Success);
        if (full)
        {
            Assert.Equal(beforeBag, _bag);
            Assert.Equal(beforeEquipment, _equipment);
            Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
            return;
        }
        Assert.Equal(weapon, Assert.Single(EquippedItems).InstanceId);
        Assert.Equal(sword, BagItems.Single(item => item.SlotIndex == source).InstanceId);
        Assert.Contains(BagItems, item => item.InstanceId == shield);
        Assert.True(_bag.GetRevision() > beforeBag.GetRevision());
        Assert.True(_equipment.GetRevision() > beforeEquipment.GetRevision());
        Assert.Equal(_bag.GetRevision(), _equipment.GetRevision());
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(shield, Assert.Single(EquippedItems).InstanceId);
        Assert.Contains(BagItems, item => item.InstanceId == weapon);
        Assert.Contains(BagItems, item => item.InstanceId == sword);
    }

    /// <summary>role-equipment：盾牌穿戴收回全部双手武器，满包使用来源格，双版本与重启保持实例。</summary>
    [Theory]
    [InlineData(1013, false)]
    [InlineData(1013, true)]
    [InlineData(1015, false)]
    [InlineData(1015, true)]
    [InlineData(1016, false)]
    [InlineData(1016, true)]
    [InlineData(1017, false)]
    [InlineData(1017, true)]
    [InlineData(1018, false)]
    [InlineData(1018, true)]
    public void ShieldAutomaticallyReturnsTwoHandedMainWeapon(int itemId, bool full)
    {
        var weapon = Grant(itemId);
        Assert.True(Equip(weapon, EquipmentSlotPb.MainHand).Success);
        if (full) Grant(1012);
        var shield = Grant(1014);
        var source = BagItems.Single(item => item.InstanceId == shield).SlotIndex;
        var beforeBag = _bag.Clone().WithRevision(_bag.GetRevision());
        var beforeEquipment = _equipment.Clone().WithRevision(_equipment.GetRevision());
        _server.Flush();
        var beforeNextId = _repo.Load(_roleId).Bag.NextInstanceId;
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        Assert.Equal(shield, Assert.Single(EquippedItems).InstanceId);
        Assert.Equal(EquipmentSlot.OffHand, EquippedItems[0].Slot);
        Assert.Equal(weapon, BagItems.Single(item => item.SlotIndex == source).InstanceId);
        Assert.Equal(beforeBag.Items.Count(item => !item.Equipped), BagItems.Count);
        Assert.True(_bag.GetRevision() > beforeBag.GetRevision());
        Assert.True(_equipment.GetRevision() > beforeEquipment.GetRevision());
        Assert.Equal(_bag.GetRevision(), _equipment.GetRevision());
        Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
        var acceptedBag = _bag.Clone().WithRevision(_bag.GetRevision());
        var acceptedEquipment = _equipment.Clone().WithRevision(_equipment.GetRevision());
        _server.Flush();
        Assert.Equal(beforeNextId, _repo.Load(_roleId).Bag.NextInstanceId);
        _server = CreateServer();
        Enter(_roleId);
        Assert.Equal(acceptedBag, _bag);
        Assert.Equal(acceptedEquipment, _equipment);
    }

    /// <summary>role-equipment：非法副手或双手组合直接拒绝选角，不迁移或重写存档。</summary>
    [Theory]
    [InlineData(0, 1018)]
    [InlineData(1017, 1017)]
    [InlineData(1013, 1014)]
    public void InvalidStoredHandsRejectSelectionWithoutRewriting(int mainId, int offId)
    {
        var role = _repo.Load(_roleId);
        var equipment = new List<DataServerEquipment>();
        if (mainId != 0) equipment.Add(new(1, mainId, EquipmentSlot.MainHand));
        equipment.Add(new(2, offId, EquipmentSlot.OffHand));
        _repo.Save(role with { Bag = role.Bag with { NextInstanceId = 3, Equipment = equipment } });
        var path = _repo.GetItemPath(_roleId);
        var original = File.ReadAllText(path);
        _server = CreateServer();
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = _roleId });
        var response = _messages.OfType<SelectRoleRt>().Last();
        Assert.False(response.Success);
        Assert.NotEmpty(response.Error);
        _server.Flush();
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal(equipment, _repo.Load(_roleId).Bag.Equipment);
    }
    /// <summary>role-equipment：GM 发放与格子移动不覆盖已穿装备，落地后保留实例。</summary>
    [Fact]
    public void GrantsAndMovesKeepEquipment()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var shield = Grant(1014);
        Send(new MoveItem { RoleId = _roleId, InstanceId = shield, TargetSlotIndex = 1, ExpectedRevision = _bag.GetRevision() });
        Assert.True(_messages.OfType<MoveItemRt>().Last().Success);
        Assert.Equal(sword, Assert.Single(EquippedItems).InstanceId);
        _server.Flush();
        Assert.Equal(sword, Assert.Single(_repo.Load(_roleId).Bag.Equipment).InstanceId);
    }
    /// <summary>role-equipment：整理不广播公开装备，每次私有组合快照共享新的信封版本。</summary>
    [Fact]
    public void InventoryEnvelopeRevisionRejectsEitherStaleVersion()
    {
        var sword = Grant(1012);
        var initial = _bag.GetRevision();
        Assert.True(initial > 0);
        Assert.Equal(initial, _equipment.GetRevision());
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var equipment = _equipment.Clone();
        var equipRevision = _equipment.GetRevision();
        var shield = Grant(1014);
        _messages.Clear();
        Send(new MoveItem { RoleId = _roleId, InstanceId = shield, TargetSlotIndex = 1, ExpectedRevision = _bag.GetRevision() });
        Assert.True(Assert.Single(_messages.OfType<MoveItemRt>()).Success);
        Assert.Equal(equipment, _equipment);
        Assert.True(_equipment.GetRevision() > equipRevision);
        Assert.Equal(_bag.GetRevision(), _equipment.GetRevision());
        Assert.Empty(_messages.OfType<RoleEquipmentNotice>());
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand, equipmentRevision: equipRevision).Success);
        Assert.Empty(_messages.OfType<RoleItemsChangedNotice>());
        Assert.False(Equip(shield, EquipmentSlotPb.OffHand, revision: initial).Success);
        Assert.True(Equip(shield, EquipmentSlotPb.OffHand).Success);
        var notice = Assert.Single(_messages.OfType<RoleItemsChangedNotice>());
        Assert.Equal(notice.Bag.GetRevision(), notice.Equipment.GetRevision());
        _server.Flush();
        Assert.Equal(notice.Equipment, MockServer.CreateEquipmentNotice(_repo.Load(_roleId)).Equipment);
        Enter(_roleId);
        Assert.Equal(notice.Equipment, _equipment);
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
        Assert.Empty(items.Bag.Items.Where(item => !item.Equipped));
        Assert.Equal(sword, Assert.Single(items.Bag.Items.Where(item => item.Equipped)).InstanceId);
        Assert.True(items.Bag.GetRevision() > 0);
        Assert.Equal(items.Bag.GetRevision(), items.Equipment.GetRevision());
    }

    /// <summary>server-write-back：穿脱落地失败保留完整内存和待写，两侧在重试后共同恢复。</summary>
    [Fact]
    public void FailedEquipmentFlushRetriesTheWholeInventory()
    {
        var sword = Grant(1012);
        Assert.True(Equip(sword, EquipmentSlotPb.MainHand).Success);
        var path = _repo.GetItemPath(_roleId);
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            Assert.Throws<AggregateException>(() => _server.Flush());
            Assert.Equal(sword, Assert.Single(EquippedItems).InstanceId);
            Assert.Empty(BagItems);
        }
        finally
        {
            Directory.Delete(path);
            File.WriteAllText(path, original);
        }
        _server.Flush();
        var saved = _repo.Load(_roleId).Bag;
        Assert.Equal(0, saved.Revision);
        Assert.Equal(0, saved.EquipmentRevision);
        Assert.Equal(sword, Assert.Single(saved.Equipment).InstanceId);
        Assert.Empty(saved.Items);
    }

    /// <summary>mock-server：装备扩展表空载荷保留文件路径诊断，不自动重建表。</summary>
    [Fact]
    public void NullEquipmentTableReportsItsSavePath()
    {
        var path = _repo.GetEquipmentPath(_roleId);
        File.WriteAllText(path, "null");
        var error = Assert.Throws<InvalidDataException>(() => _repo.Load(_roleId));
        Assert.Contains(path, error.Message);
        Assert.Equal("null", File.ReadAllText(path));
    }
    /// <summary>清理本次隔离存档。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
