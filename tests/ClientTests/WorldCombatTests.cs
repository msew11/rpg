using Godot;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Protocol;
using Rpg.Session.Data;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>对应 openspec/specs/client-ecs-world：无服务推进攻击、输入顺序、校正及权威生命。</summary>
[Trait("Runtime", "Godot")]
public sealed class WorldCombatTests
{
    /// <summary>只给世界输入数据即可走近，权威位置未入范围时不能起手，预测不扣血。</summary>
    [Fact]
    public void ApproachUsesMovementAndWaitsForAuthorityBeforePredicting()
    {
        var monster = Monster(200);
        var world = Create(monster);
        world.Enqueue(new SetCombatIntentAction(monster.ObjectId));
        world.FlushActions();
        var move = Assert.IsType<DataMoveRequested>(Assert.Single(world.TakeEvents()));
        Assert.InRange(move.Action.Destination.X, 151.9f, 152.1f);
        Assert.Equal(0, world.ActorState!.AttackDurationMs);
        world.Tick(1);
        Assert.Empty(world.TakeEvents());
        Assert.Equal(move.Action.Destination, world.RolePosition);
        world.Enqueue(Context(world.RolePosition));
        world.Tick(0);
        var attack = Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents()));
        Assert.True(attack.AttackId > (ulong)move.Action.ActionId);
        Assert.Equal(monster.ObjectId, attack.Target);
        Assert.Equal(RoleMotion.Attack1, world.ActorState!.Motion);
        Assert.Equal(Vector2.Right, world.ActorState.Facing);
        Assert.Equal(1000, world.ActorState.AttackDurationMs);
        Assert.Equal(100, world.MapObjects!.Objects[monster.ObjectId].Health);
        Assert.Empty(world.TakeDamageNumbers());
    }

    /// <summary>动画结束仍等待对应响应，拒绝撤销预测并按现有100ms间隔重试。</summary>
    [Fact]
    public void AnimationEndDoesNotReleasePendingRequestAndRejectionIsCorrelated()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var first = Start(world, monster.ObjectId);
        world.Tick(1.1);
        Assert.Empty(world.TakeEvents());
        Assert.Equal(0, world.ActorState!.AttackDurationMs);
        world.Enqueue(new SyncAttackReplyAction(new(first.AttackId + 1, false, "旧响应", null)));
        world.Tick(0);
        Assert.Empty(world.TakeEvents());
        world.Enqueue(new SyncAttackReplyAction(new(first.AttackId, false, "拒绝", null)));
        world.Tick(0);
        Assert.Empty(world.TakeEvents());
        world.Tick(.099);
        Assert.Empty(world.TakeEvents());
        world.Tick(.002);
        Assert.True(Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents())).AttackId > first.AttackId);
    }

    /// <summary>取消与移动即时有序输出，迟到起手及确认不能复活动作或提前冷却。</summary>
    [Fact]
    public void CancellationKeepsCooldownAndRejectsLateAuthority()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var request = Start(world, monster.ObjectId);
        var attack = world.Entities.Get<DataAttackComponent>(world.RoleEntity).Current!;
        world.Tick(.2);
        world.Enqueue(new CancelCombatAction());
        world.FlushActions();
        var cancel = Assert.IsType<DataAttackCancelRequested>(Assert.Single(world.TakeEvents()));
        Assert.True(cancel.ActionId > request.AttackId);
        world.Enqueue(new SyncAttackStartedAction(attack));
        world.Enqueue(new SyncAttackReplyAction(new(request.AttackId, true, "", attack)));
        world.FlushActions();
        Assert.Equal(0, world.ActorState!.AttackDurationMs);
        Assert.Null(world.AttackTarget);
        world.Enqueue(new SetCombatIntentAction(monster.ObjectId));
        world.FlushActions();
        Assert.IsType<DataMoveRequested>(Assert.Single(world.TakeEvents()));
        world.Tick(.799);
        Assert.Empty(world.TakeEvents());
        world.Tick(.002);
        Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents()));
    }

    /// <summary>追击中取消在输入阶段依次输出取消和停止，时间不前进且事件只消费一次。</summary>
    [Fact]
    public void ChaseCancellationFlushesCancelBeforeStopWithoutAdvancingTime()
    {
        var monster = Monster(200);
        var world = Create(monster);
        world.Enqueue(new SetCombatIntentAction(monster.ObjectId));
        world.FlushActions();
        world.TakeEvents();
        var now = world.Time.Now;
        world.Enqueue(new CancelCombatAction());
        world.FlushActions();
        var events = world.TakeEvents();
        var cancel = Assert.IsType<DataAttackCancelRequested>(events[0]);
        var stop = Assert.IsType<DataMoveRequested>(events[1]);
        Assert.Equal(2, events.Length);
        Assert.True((ulong)stop.Action.ActionId > cancel.ActionId);
        Assert.Equal(world.RolePosition, stop.Action.Destination);
        Assert.Equal(now, world.Time.Now);
        Assert.Empty(world.TakeEvents());
        world.Tick(.1);
        Assert.Null(world.RoleTarget);
    }

    /// <summary>权威时序校正进入同一快照，属性更新不修改已起手的预测快照。</summary>
    [Fact]
    public void ConfirmedTimingAndAttributesUseOneWorldSnapshot()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var request = Start(world, monster.ObjectId);
        var original = world.Entities.Get<DataAttackComponent>(world.RoleEntity).Current!;
        world.Enqueue(Context(Vector2.Zero) with { AttacksPerSecond = 2, AttackMotion = RoleMotion.Attack2 });
        world.Tick(.1);
        Assert.Equal(1000, world.ActorState!.AttackDurationMs);
        Assert.Equal(RoleMotion.Attack1, world.ActorState.Motion);
        var accepted = original with { StartMs = original.StartMs + 50, DurationMs = 1200, HitTimeMs = original.StartMs + 410 };
        world.Enqueue(new SyncAttackStartedAction(accepted));
        world.Enqueue(new SyncAttackReplyAction(new(request.AttackId, true, "", accepted)));
        world.Tick(0);
        Assert.Equal(1200, world.ActorState.AttackDurationMs);
        Assert.Equal(50, world.ActorState.AttackElapsedMs);
        world.Tick(1.151);
        Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents()));
        Assert.Equal(RoleMotion.Attack2, world.ActorState.Motion);
        Assert.Equal(500, world.ActorState.AttackDurationMs);
    }

    /// <summary>远端攻击可先于实体到达，旧位置动作和旧控制取消不覆盖新攻击。</summary>
    [Fact]
    public void RemoteAttackLivesInEcsAndSurvivesBaseMotionUpdates()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var old = new DataAttack(2, 10, monster.ObjectId, 1000, 1000, 1300, RoleMotion.Attack2, new(20, 0));
        world.Enqueue(new SyncAttackStartedAction(old));
        world.Enqueue(new SyncRemoteActorsAction([new(2, RoleGender.Female, Vector2.Zero, RoleMotion.Stand, Vector2.Down, 0)]));
        world.Tick(.1);
        var state = Assert.Single(world.RemoteActors);
        Assert.Equal(RoleMotion.Attack2, state.Motion);
        Assert.Equal(100, state.AttackElapsedMs);
        world.Enqueue(new SyncAttackCancelledAction(2, 10, 1000));
        world.Enqueue(new SyncAttackStartedAction(old));
        world.Tick(0);
        Assert.Equal(0, Assert.Single(world.RemoteActors).AttackDurationMs);
        var current = old with { AttackId = 1, StartMs = 3000, HitTimeMs = 3300 };
        world.Enqueue(Context(Vector2.Zero) with { ServerTimeMs = 3100 });
        world.Enqueue(new SyncAttackStartedAction(current));
        world.Enqueue(new SyncAttackCancelledAction(2, 10, 1000));
        world.Enqueue(new SyncRemoteActorsAction([state with { Motion = RoleMotion.Run }]));
        world.Tick(0);
        Assert.Equal(RoleMotion.Attack2, Assert.Single(world.RemoteActors).Motion);
        Assert.Equal(100, Assert.Single(world.RemoteActors).AttackElapsedMs);
        world.Enqueue(new SyncRemoteActorsAction([]));
        world.Tick(0);
        Assert.Empty(world.RemoteActors);
    }

    /// <summary>怪物生命按版本更新，死亡墓碑跨AOI移除保留，重复命中不重复跳字。</summary>
    [Fact]
    public void HealthRejectsOldSnapshotsAndLethalTextSurvivesEntityRemoval()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var wounded = monster with { Revision = 2, Health = 80 };
        var hit = new SyncCombatHitAction(new(1, 1, 20, new(20, 0), false), wounded, 1100);
        world.Enqueue(hit);
        world.Enqueue(new SyncMapObjectsAction(Snapshot(monster)));
        world.Tick(0);
        var state = world.MapObjects!.Objects[monster.ObjectId];
        Assert.Equal(80, state.Health);
        Assert.Equal(100, state.MaxHealth);
        Assert.Single(world.TakeDamageNumbers());
        world.Enqueue(new SyncMapObjectsAction(Snapshot(monster with { Health = 0 })));
        world.Tick(0);
        Assert.Equal(80, world.MapObjects.Objects[monster.ObjectId].Health);
        world.Enqueue(hit);
        world.Tick(0);
        Assert.Empty(world.TakeDamageNumbers());
        var dead = wounded with { Revision = 3, Health = 0 };
        world.Enqueue(new SetCombatIntentAction(monster.ObjectId));
        world.FlushActions();
        world.TakeEvents();
        world.Enqueue(new SyncCombatHitAction(new(2, 2, 80, new(20, 0), true), dead, 1200));
        world.Enqueue(new SyncMapObjectsAction(Snapshot(monster)));
        world.Tick(0);
        Assert.Empty(world.MapObjects.Objects);
        Assert.Null(world.AttackTarget);
        var lethal = Assert.Single(world.TakeDamageNumbers());
        Assert.True(lethal.Dead);
        Assert.Equal(new Vector2(20, 0), lethal.Position);
        world.Enqueue(new SyncMapObjectsAction(null));
        world.Enqueue(new SyncMapObjectsAction(Snapshot(wounded)));
        world.Tick(0);
        Assert.Empty(world.MapObjects!.Objects);
    }

    /// <summary>全量重同步会重置AOI内部序号，仍必须接纳新的对象集合。</summary>
    [Fact]
    public void FullResyncReplacesAoiEvenWhenItsInternalSequenceRestarts()
    {
        var monster = Monster(20);
        var world = Create(monster);
        var incremental = Snapshot(monster).Apply(new MapAoiDeltaPb { Center = new() }, 9);
        world.Enqueue(new SyncMapObjectsAction(incremental));
        world.Tick(0);
        Assert.Equal(9, world.MapObjects!.Sequence);
        var entered = Monster(40);
        world.Enqueue(new SyncMapObjectsAction(Snapshot(entered)));
        world.Tick(0);
        Assert.Equal(entered.ObjectId, Assert.Single(world.MapObjects.Objects).Key);
        Assert.Equal(1, world.MapObjects.Sequence);
    }

    /// <summary>换装取消规则由世界处理，保留冷却且旧装备版本不能取消新循环。</summary>
    [Fact]
    public void EquipmentChangeCancelsInEcsAndPreservesExistingInterval()
    {
        var monster = Monster(20);
        var world = Create(monster);
        world.Enqueue(new SyncEquipmentAction(1, 1, 0, 0));
        world.FlushActions();
        Start(world, monster.ObjectId);
        world.Tick(.2);
        world.Enqueue(new SyncEquipmentAction(1, 2, 1012, 0));
        world.FlushActions();
        Assert.IsType<DataAttackCancelRequested>(Assert.Single(world.TakeEvents()));
        Assert.Null(world.AttackTarget);
        Assert.Equal(0, world.ActorState!.AttackDurationMs);
        world.Enqueue(new SetCombatIntentAction(monster.ObjectId));
        world.FlushActions();
        world.TakeEvents();
        world.Enqueue(new SyncEquipmentAction(1, 1, 0, 0));
        world.Tick(.799);
        Assert.Empty(world.TakeEvents());
        Assert.Equal(monster.ObjectId, world.AttackTarget);
        world.Tick(.002);
        Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents()));
    }

    /// <summary>构造只含纯数据的世界，不加载配置、会话或网络。</summary>
    private static WorldManager Create(DataMapObjectPbData monster)
    {
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(new MapCfg { Width = 16, Height = 16, TileSize = 32 })));
        world.Enqueue(new InitializeRoleAction(new(1, "测试", 1, 100, 100, 0, 0, 0, 1, Vector2.Zero, RoleGender.Male, 1, 0)));
        world.Enqueue(new SyncMapObjectsAction(Snapshot(monster)));
        world.Enqueue(Context(Vector2.Zero));
        world.Tick(0);
        return world;
    }

    /// <summary>提供可控时间、位置和攻击属性。</summary>
    private static SyncCombatContextAction Context(Vector2 authority)
        => new(1000, authority, true, 180, 48, 1, RoleMotion.Attack1, .3);

    /// <summary>建立已知血量和稳定服务器身份的怪物。</summary>
    private static DataMapObjectPbData Monster(float x)
        => new(Guid.NewGuid(), 1, x, 0, "1001", 0, 0) { Health = 100, MaxHealth = 100 };

    /// <summary>构造完整单块AOI快照，用于直接验证世界的版本保护。</summary>
    private static DataMapAoiSnapshotPbData Snapshot(DataMapObjectPbData monster)
    {
        var snapshot = new MapAoiSnapshotPb { MapId = 1,
            Layout = new() { Width = 16, Height = 16, TileSize = 32, ChunkSizeTiles = 16 }, Center = new() };
        var chunk = new MapChunkPb { Coordinate = new() };
        chunk.Objects.Add(new MapObjectPb { ObjectId = monster.ObjectId.ToString("N"), Revision = monster.Revision,
            X = monster.X, Y = monster.Y, Monster = new() { MonsterTypeId = monster.MonsterTypeId, Health = monster.Health, MaxHealth = monster.MaxHealth } });
        snapshot.Chunks.Add(chunk);
        return DataMapAoiSnapshotPbData.FromPb(snapshot, 1);
    }

    /// <summary>通过公开输入阶段建立意图，固定帧产生一次起手。</summary>
    private static DataAttackRequested Start(WorldManager world, Guid target)
    {
        world.Enqueue(new SetCombatIntentAction(target));
        world.FlushActions();
        world.TakeEvents();
        world.Tick(0);
        return Assert.IsType<DataAttackRequested>(Assert.Single(world.TakeEvents()));
    }
}
