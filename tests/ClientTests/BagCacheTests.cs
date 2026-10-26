using Rpg.Net.Message;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/role-bag 的完整替换、不可变快照、版本过滤与切角隔离。</summary>
[Trait("Runtime", "Godot")]
public sealed class BagCacheTests : RegisteredServiceTestScope
{
    /// <summary>造出当前正式配置中存在的单个装备快照。</summary>
    private static BagPb Snapshot(long revision, int slot = 0) => new()
    {
        Revision = revision,
        Items = { new ItemPb { InstanceId = 1, ItemId = 1012, SlotIndex = slot, Count = 1 } }
    };

    /// <summary>创建仅本人的组合通知，普通背包更新带上未变化的空装备。</summary>
    private static RoleItemsChangedNotice Notice(BagPb? bag, long roleId = 1001,
        RoleWorldEquipmentPb? equipment = null) => new()
    {
        RoleId = roleId, Bag = bag, Equipment = equipment ?? new RoleWorldEquipmentPb { RoleId = roleId }
    };

    /// <summary>完整替换在普通监听前发生，协议变动不污染缓存，旧版本和其他角色被忽略。</summary>
    [Fact]
    public void ReplacesValidatedSnapshotBeforeUiAndClearsOnRoleSwitch()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var message = Snapshot(1);
        var observed = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ => observed = session.Bag.Revision == 1);
        EventBus.Publish(Notice(message));
        Assert.True(observed);
        Assert.Equal(1012, Assert.Single(session.Bag.Items).ItemId);
        var previous = session.Bag;
        message.Items[0].Count = 99;
        Assert.Equal(1, session.Bag.Items[0].Count);
        var output = previous.ToPb();
        output.Items.Clear();
        Assert.Single(previous.Items);
        EventBus.Publish(Notice(Snapshot(1, 2)));
        EventBus.Publish(Notice(Snapshot(99), 1002));
        Assert.Same(previous, session.Bag);
        EventBus.Publish(Notice(new BagPb { Revision = 2 }));
        Assert.Empty(session.Bag.Items);
        session.LeaveRole();
        Assert.Throws<InvalidOperationException>(() => session.Bag);
        session.SelectRole(1002);
        fixture.Net.Tick(0);
        Assert.Empty(session.Bag.Items);
        Assert.Equal(0, session.Bag.Revision);
        EventBus.Publish(Notice(Snapshot(100)));
        Assert.Empty(session.Bag.Items);
    }

    /// <summary>非法完整快照不允许部分更新，也不进入普通阶段。</summary>
    [Theory]
    [InlineData("duplicateSlot")]
    [InlineData("duplicateId")]
    [InlineData("unknown")]
    [InlineData("count")]
    [InlineData("outside")]
    [InlineData("negative")]
    [InlineData("missing")]
    public void InvalidSnapshotPreservesPreviousAndSkipsUi(string kind)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var previous = session.Bag;
        var message = Snapshot(1);
        switch (kind)
        {
            case "duplicateSlot": message.Items.Add(new ItemPb { InstanceId = 2, ItemId = 1012, Count = 1, SlotIndex = 0 }); break;
            case "duplicateId": message.Items.Add(new ItemPb { InstanceId = 1, ItemId = 1012, Count = 1, SlotIndex = 1 }); break;
            case "unknown": message.Items[0].ItemId = 9999; break;
            case "count": message.Items[0].Count = 2; break;
            case "outside": message.Items[0].SlotIndex = 40; break;
            case "negative": message.Items[0].Count = -1; break;
        }
        var called = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ => called = true);
        EventBus.Publish(Notice(kind == "missing" ? null : message));
        Assert.False(called);
        Assert.Single(fixture.Errors);
        Assert.Same(previous, session.Bag);
    }

    /// <summary>选角容量或背包非法时不提前发布角色状态。</summary>
    [Fact]
    public void InvalidSelectionDoesNotPartiallyInitializeRole()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var state = session.Role.ToPb();
        session.RoleCache.Reset(1001);
        EventBus.Publish(new SelectRoleRt { RoleId = 1001, Success = true, State = state,
            Role = new RolePb { RoleId = 1001, BagExtraRows = int.MaxValue, NextExperience = 1 }, Bag = new BagPb() });
        Assert.False(session.HasRole);
        Assert.Single(fixture.Errors);
    }

    /// <summary>role-equipment：两侧共用完整校验，重复实例或不适配装备不得留下半份快照。</summary>
    [Theory]
    [InlineData("sharedInstance")]
    [InlineData("duplicateSlot")]
    [InlineData("unknownSlot")]
    [InlineData("wrongCategory")]
    public void InvalidEquipmentPreservesWholeInventory(string kind)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var previous = session.Bag;
        var message = Snapshot(1);
        var equipment = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 1 };
        equipment.Items.Add(new EquippedItemPb { InstanceId = kind == "sharedInstance" ? 1 : 2,
            ItemId = kind == "wrongCategory" ? 1014 : 1012,
            Slot = kind == "unknownSlot" ? (EquipmentSlotPb)999 : EquipmentSlotPb.MainHand });
        if (kind == "duplicateSlot") equipment.Items.Add(new EquippedItemPb { InstanceId = 3, ItemId = 1017, Slot = EquipmentSlotPb.MainHand });
        var called = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ => called = true);
        EventBus.Publish(Notice(message, equipment: equipment));
        Assert.False(called);
        Assert.Single(fixture.Errors);
        Assert.Same(previous, session.Bag);
    }

    /// <summary>装备协议复制后不可变，普通阶段同时看到背包和装备的新版本。</summary>
    [Fact]
    public void EquipmentSnapshotsAreCopiedAndAcceptedAtomically()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var message = Snapshot(1);
        var equipment = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 1 };
        equipment.Items.Add(new EquippedItemPb { InstanceId = 2, ItemId = 1017, Slot = EquipmentSlotPb.MainHand });
        var called = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ => called = session.Bag.Items.Count == 1 && session.Equipment.Items.Count == 1);
        EventBus.Publish(Notice(message, equipment: equipment));
        Assert.True(called);
        equipment.Items[0].ItemId = 1014;
        session.Equipment.ToPb().Items.Clear();
        Assert.Equal(1017, Assert.Single(session.Equipment.Items).ItemId);
    }
}
