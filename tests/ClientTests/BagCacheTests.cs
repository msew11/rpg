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

    /// <summary>完整替换在普通监听前发生，协议变动不污染缓存，旧版本和其他角色被忽略。</summary>
    [Fact]
    public void ReplacesValidatedSnapshotBeforeUiAndClearsOnRoleSwitch()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var message = Snapshot(1);
        var observed = false;
        using var ui = EventBus.Subscribe<BagChangedNotice>(_ => observed = session.Bag.Revision == 1);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = message });
        Assert.True(observed);
        Assert.Equal(1012, Assert.Single(session.Bag.Items).ItemId);
        var previous = session.Bag;
        message.Items[0].Count = 99;
        Assert.Equal(1, session.Bag.Items[0].Count);
        var output = previous.ToPb();
        output.Items.Clear();
        Assert.Single(previous.Items);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = Snapshot(1, 2) });
        EventBus.Publish(new BagChangedNotice { RoleId = 1002, Bag = Snapshot(99) });
        Assert.Same(previous, session.Bag);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 2 } });
        Assert.Empty(session.Bag.Items);
        session.LeaveRole();
        Assert.Throws<InvalidOperationException>(() => session.Bag);
        session.SelectRole(1002);
        fixture.Net.Tick(0);
        Assert.Empty(session.Bag.Items);
        Assert.Equal(0, session.Bag.Revision);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = Snapshot(100) });
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
        using var ui = EventBus.Subscribe<BagChangedNotice>(_ => called = true);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = kind == "missing" ? null : message });
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
        message.Equipment.Add(new EquippedItemPb { InstanceId = kind == "sharedInstance" ? 1 : 2,
            ItemId = kind == "wrongCategory" ? 1018 : 1012,
            Slot = kind == "unknownSlot" ? (EquipmentSlotPb)999 : EquipmentSlotPb.MainHand });
        if (kind == "duplicateSlot") message.Equipment.Add(new EquippedItemPb { InstanceId = 3, ItemId = 1017, Slot = EquipmentSlotPb.MainHand });
        var called = false;
        using var ui = EventBus.Subscribe<BagChangedNotice>(_ => called = true);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = message });
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
        message.Equipment.Add(new EquippedItemPb { InstanceId = 2, ItemId = 1017, Slot = EquipmentSlotPb.OffHand });
        var called = false;
        using var ui = EventBus.Subscribe<BagChangedNotice>(_ => called = session.Bag.Items.Count == 1 && session.Bag.Equipment.Count == 1);
        EventBus.Publish(new BagChangedNotice { RoleId = 1001, Bag = message });
        Assert.True(called);
        message.Equipment[0].ItemId = 1014;
        session.Bag.ToPb().Equipment.Clear();
        Assert.Equal(1017, Assert.Single(session.Bag.Equipment).ItemId);
    }
}
