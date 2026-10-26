using Rpg.Net.Message;
using Rpg.Services;
using Rpg.World;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session、role-equipment 的公私交错、原子接纳及独立世界同步。</summary>
[Trait("Runtime", "Godot")]
public sealed class RoleEquipmentCacheTests : RegisteredServiceTestScope
{
    /// <summary>构造可独立公开的单件主手快照。</summary>
    private static RoleWorldEquipmentPb Equipment(long revision = 1) => new()
    {
        RoleId = 1001, Revision = revision,
        Items = { new EquippedItemPb { InstanceId = 1, ItemId = 1012, Slot = EquipmentSlotPb.MainHand } }
    };

    /// <summary>任一侧或装备版本非法，均不得发布半份本人物品状态。</summary>
    [Theory]
    [InlineData("rollback")]
    [InlineData("conflict")]
    [InlineData("identity")]
    [InlineData("missingEquipment")]
    [InlineData("missingBag")]
    [InlineData("sharedInstance")]
    [InlineData("bothHands")]
    public void InvalidInventoryCommitPreservesBothSnapshots(string kind)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 1 }, Equipment = Equipment() });
        var beforeBag = session.Bag;
        var beforeEquipment = session.Equipment;
        var notice = new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 2 }, Equipment = Equipment() };
        switch (kind)
        {
            case "rollback": notice.Equipment.Revision = 0; break;
            case "conflict": notice.Equipment.Items[0].ItemId = 1017; break;
            case "identity": notice.Equipment.RoleId = 1002; break;
            case "missingEquipment": notice.Equipment = null; break;
            case "missingBag": notice.Bag = null; break;
            case "sharedInstance": notice.Bag.Items.Add(new ItemPb { InstanceId = 1, ItemId = 1012, Count = 1 }); break;
            case "bothHands":
                notice.Equipment.Revision = 2;
                notice.Equipment.Items[0].ItemId = 1018;
                notice.Equipment.Items.Add(new EquippedItemPb { InstanceId = 2, ItemId = 1014, Slot = EquipmentSlotPb.OffHand });
                break;
        }
        var called = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ => called = true);
        EventBus.Publish(notice);
        Assert.False(called);
        Assert.Single(fixture.Errors);
        Assert.Same(beforeBag, session.Bag);
        Assert.Same(beforeEquipment, session.Equipment);
        Assert.Same(beforeEquipment, session.Role.Equipment);
    }

    /// <summary>独立和完整公开消息不能绕过本人组合提交，其他公开领域仍照常更新。</summary>
    [Fact]
    public void PublicUpdatesCannotSplitOwnerInventoryAndOlderCommitsAreIgnored()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var beforeBag = session.Bag;
        var beforeEquipment = session.Equipment;
        EventBus.Publish(new RoleEquipmentNotice { Equipment = Equipment() });
        var world = session.Role.ToPb();
        world.Equipment = Equipment();
        world.Profile.Revision++;
        world.Profile.Name = "新名称";
        EventBus.Publish(new RoleStateNotice { State = world });
        Assert.Equal("新名称", session.Role.Name);
        Assert.Same(beforeEquipment, session.Equipment);
        Assert.Same(beforeBag, session.Bag);
        var observedAtomically = false;
        using var ui = EventBus.Subscribe<RoleItemsChangedNotice>(_ =>
            observedAtomically = session.Bag.Revision == 1 && session.Equipment.Revision == 1 &&
                session.Role.ToPb().Equipment.Equals(Equipment()));
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 1 }, Equipment = Equipment() });
        Assert.True(observedAtomically);
        var current = session.Equipment;
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 1 }, Equipment = Equipment(99) });
        Assert.Same(current, session.Equipment);
        world.Profile.Revision++;
        world.Equipment = new RoleWorldEquipmentPb { RoleId = 1001 };
        EventBus.Publish(new RoleStateNotice { State = world });
        Assert.Same(current, session.Equipment);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>初始协议缺少任一物品领域时，不能留下半初始化的角色缓存。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectionRequiresBothEquipmentAndBag(bool missingEquipment)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        var state = session.Role.ToPb();
        var role = session.Role.ToRolePb();
        session.RoleCache.Reset(1001);
        if (missingEquipment) state.Equipment = null;
        EventBus.Publish(new SelectRoleRt { Success = true, RoleId = 1001, State = state, Role = role,
            Bag = missingEquipment ? new BagPb() : null });
        Assert.False(session.HasRole);
        Assert.Throws<InvalidOperationException>(() => session.Equipment);
        Assert.Throws<InvalidOperationException>(() => session.Bag);
        Assert.Single(fixture.Errors);
    }

    /// <summary>装备独立驱动 ECS，背包整理保留装备引用，卸下和切角清除旧持握。</summary>
    [Fact]
    public void WorldUsesIndependentEquipmentAndRoleSwitchClearsIt()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var session = fixture.SelectRole(1001);
        using var world = new WorldService(GameServices.Env, GameServices.Config, GameServices.Setting, session);
        world.InitializeRole();
        Assert.Equal(0, world.ActorState!.MainHandId);
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 1 }, Equipment = Equipment() });
        world.Tick(0);
        Assert.Equal(1012, world.ActorState!.MainHandId);
        var equipment = session.Equipment;
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 2 }, Equipment = Equipment() });
        world.Tick(0);
        Assert.Same(equipment, session.Equipment);
        Assert.Equal(1012, world.ActorState!.MainHandId);
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 3 },
            Equipment = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 2 } });
        world.Tick(0);
        Assert.Equal(0, world.ActorState!.MainHandId);
        world.LeaveRole();
        session.LeaveRole();
        fixture.SelectRole(1002);
        world.InitializeRole();
        Assert.Empty(session.Equipment.Items);
        Assert.Equal(0, world.ActorState!.MainHandId);
        EventBus.Publish(new RoleItemsChangedNotice { RoleId = 1001, Bag = new BagPb { Revision = 99 }, Equipment = Equipment(99) });
        Assert.Empty(session.Equipment.Items);
        Assert.Empty(fixture.Errors);
    }
}
