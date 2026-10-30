using Rpg.Config.Data;
using Rpg.Services;
using Rpg.Const;
using Rpg.Session.Data;
using Godot;
using Rpg.Protocol;
using Rpg.Session;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session 与 client-actor-presentation 的无节点角色逻辑场景。</summary>
[Trait("Runtime", "Godot")]
public sealed class ActorAcceptanceTests : RegisteredServiceTestScope
{
    /// <summary>男女角色的性别从登录、会话和确认缓存进入玩家 entity。</summary>
    [Theory]
    [InlineData(1001, RoleGender.Male)]
    [InlineData(1002, RoleGender.Female)]
    public void GenderFlowsFromSessionIntoEntity(long roleId, RoleGender gender)
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        Assert.Equal(gender, source.Login("demo1", "123").Roles.Single(role => role.RoleId == roleId).Gender);
        using var session = source.SelectRole(roleId);
        var world = CreateWorld(session.Role);
        Assert.Equal(gender, session.RoleCache.State.Gender);
        Assert.Equal(gender, world.ActorState!.Gender);
        using var subscription = EventBus.Subscribe<MoveRt>(_ => world.Enqueue(new SyncRoleAction(session.RoleCache.State)));
        session.SubmitMove(1, session.Role.Position + new Vector2(100, 0));
        source.Net.Tick(0.5);
        world.Tick(0);
        Assert.Equal(gender, session.RoleCache.State.Gender);
        Assert.Equal(gender, world.ActorState.Gender);
    }

    /// <summary>移动、改向、到达和权威修正只依靠逻辑状态，旧快照不可被组件变更污染。</summary>
    [Fact]
    public void MovementSelectsMotionAndRetainsFacingOnStop()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        using var session = source.SelectRole(source.Login().Roles[0].RoleId);
        var role = session.Role;
        var world = CreateWorld(role);
        var initial = world.ActorState!;
        Assert.Equal(RoleMotion.Stand, initial.Motion);
        world.Enqueue(new MoveAction(1, role.Position + new Vector2(100, 0)));
        world.Tick(0);
        Assert.Equal(RoleMotion.Stand, world.ActorState!.Motion);
        world.Tick(0.1);
        Assert.Equal(RoleMotion.Run, world.ActorState!.Motion);
        Assert.Equal(Vector2.Right, world.ActorState.Facing);
        Assert.Equal(role.Position, initial.Position);
        var turnStart = world.RolePosition;
        world.Enqueue(new MoveAction(2, turnStart + new Vector2(0, 100)));
        world.Tick(0.1);
        Assert.Equal(Vector2.Down, world.ActorState.Facing);
        world.Enqueue(new SyncRoleAction(role with { Position = turnStart + new Vector2(0, 50), Revision = role.Revision + 1, PositionRevision = role.PositionRevision + 1, AcknowledgedAction = 1 }));
        world.Tick(0);
        Assert.Equal(RoleMotion.Run, world.ActorState.Motion);
        Assert.Equal(Vector2.Down, world.ActorState.Facing);
        world.Tick(1);
        Assert.Null(world.RoleTarget);
        Assert.Equal(RoleMotion.Stand, world.ActorState.Motion);
        Assert.Equal(Vector2.Down, world.ActorState.Facing);
        world.Enqueue(new MoveAction(3, world.RolePosition));
        world.Tick(1);
        Assert.Equal(Vector2.Down, world.ActorState.Facing);
    }

    /// <summary>属性更新独立于移动确认序号，旧 revision 和其他角色不能回滚性别。</summary>
    [Fact]
    public void AuthorityGenderUsesRevisionRatherThanMovementAcknowledgement()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        using var session = source.SelectRole(source.Login().Roles[0].RoleId);
        var role = session.Role;
        var world = CreateWorld(role);
        world.Enqueue(new SyncRoleAction(role with { Gender = RoleGender.Female, Revision = role.Revision + 2, ProfileRevision = role.ProfileRevision + 2, AcknowledgedAction = 0 }));
        world.Tick(0);
        Assert.Equal(RoleGender.Female, world.ActorState!.Gender);
        world.Enqueue(new SyncRoleAction(role with { Position = role.Position + Vector2.One, Revision = role.Revision + 1, PositionRevision = role.PositionRevision + 1, AcknowledgedAction = 1 }));
        world.Enqueue(new SyncRoleAction(role with { RoleId = 123, Revision = role.Revision + 3, AcknowledgedAction = 2 }));
        world.Tick(0);
        Assert.Equal(RoleGender.Female, world.ActorState.Gender);
        Assert.Equal(role.Position + Vector2.One, world.RolePosition);
    }

    /// <summary>所有非移动动作通过 Action 进入实体，重复动作可重播，后续移动接管动作。</summary>
    [Fact]
    public void NonMovementMotionsStayLogicalUntilNextCommand()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        using var session = source.SelectRole(source.Login().Roles[1].RoleId);
        var role = session.Role;
        var world = CreateWorld(role);
        foreach (var motion in Enum.GetValues<RoleMotion>().Except([RoleMotion.Stand, RoleMotion.Run]))
        {
            world.Enqueue(new MoveAction(1, role.Position + new Vector2(100, 0)));
            world.Tick(0.1);
            world.Enqueue(new SetRoleMotionAction(role.RoleId, motion));
            world.Tick(0.1);
            var state = world.ActorState!;
            Assert.Equal(motion, state.Motion);
            Assert.Null(world.RoleTarget);
            world.Tick(5);
            Assert.Equal(state, world.ActorState);
            world.Enqueue(new SetRoleMotionAction(role.RoleId, motion));
            world.Tick(0);
            Assert.Equal(state.MotionVersion + 1, world.ActorState!.MotionVersion);
            world.Enqueue(new SetRoleMotionAction(999, RoleMotion.Hurt));
            world.Tick(0);
            Assert.Equal(motion, world.ActorState.Motion);
        }
        world.Enqueue(new MoveAction(2, world.RolePosition + new Vector2(100, 0)));
        world.Tick(0.1);
        Assert.Equal(RoleMotion.Run, world.ActorState!.Motion);
    }

    /// <summary>使用真实地图配置初始化一个不创建表现节点的玩家世界。</summary>
    private static WorldManager CreateWorld(RoleWorldStatePbData role)
    {
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(new MapCfg { Width = 1000, Height = 1000, TileSize = 32 })));
        world.Enqueue(new InitializeRoleAction(role));
        world.Tick(0);
        return world;
    }
}
