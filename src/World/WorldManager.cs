using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using Rpg.Config;
using Godot;
using Rpg.Const;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Systems;
using Rpg.World.Utils;
using Rpg.Session.Data;

namespace Rpg.World;

/// <summary>管理 Action 唯一入口、ECS 实体和固定帧系统顺序。</summary>
internal sealed partial class WorldManager
{
    /// <summary>待处理的输入和权威同步消息。</summary>
    private readonly Queue<WorldAction> _actions = new();
    /// <summary>该世界的实体组件仓库。</summary>
    private readonly EntityStore _entities = new();
    /// <summary>服务器对象 GUID 对应当前世界的整数实体 ID。</summary>
    private readonly Dictionary<Guid, int> _mapEntities = new();
    /// <summary>远端玩家单独的实体索引。</summary>
    private readonly Dictionary<long, int> _remoteEntities = new();
    /// <summary>从 ECS 复制的远端公开呈现状态。</summary>
    public IReadOnlyList<DataActorState> RemoteActors => _remoteEntities.Values.Select(ReadActor).ToArray();
    /// <summary>由当前 ECS 组件生成的只读对象集合。</summary>
    public DataWorldObjectsState? MapObjects { get; private set; }
    /// <summary>固定运行顺序的逻辑系统。</summary>
    private readonly IWorldSystem[] _systems;
    /// <summary>输入阶段复用走近系统，不执行自动起手。</summary>
    private readonly CombatApproachSystem _approach = new();
    /// <summary>最终动作的唯一合成系统。</summary>
    private readonly ActorMotionSystem _motion = new();
    /// <summary>建立世界级系统实例，实体只持有自己的数据。</summary>
    internal WorldManager()
        => _systems = [_approach, new MovementSystem(), new AttackSystem(), _motion];
    /// <summary>地图世界实体 ID。</summary>
    private int _worldEntity;
    /// <summary>当前角色实体 ID。</summary>
    private int _roleEntity;
    /// <summary>最后应用的权威确认序号。</summary>
    private long _lastConfirmedAction;
    /// <summary>已接纳的角色属性权威版本。</summary>
    private long _lastProfileRevision;
    /// <summary>已接纳的位置权威版本。</summary>
    private long _lastPositionRevision;

    /// <summary>当前世界是否已创建地图实体。</summary>
    public bool HasWorld => _worldEntity != 0;
    /// <summary>当前世界是否已创建角色实体。</summary>
    public bool HasRole => _roleEntity != 0;
    /// <summary>角色的最新逻辑位置。</summary>
    public Vector2 RolePosition => HasRole ? _entities.Get<DataPositionComponent>(_roleEntity).Value : Vector2.Zero;
    /// <summary>角色当前移动目标。</summary>
    public Vector2? RoleTarget => HasRole ? _entities.Get<DataMovementComponent>(_roleEntity).Target : null;
    /// <summary>已应用的最新确认意图序号。</summary>
    public long LastConfirmedAction => _lastConfirmedAction;

    /// <summary>复制玩家逻辑状态供表现读取，不暴露可写组件。</summary>
    public DataActorState? ActorState => HasRole ? ReadActor(_roleEntity) : null;

    /// <summary>复制系统已合成的完整动作，不在世界外叠加攻击。</summary>
    private DataActorState ReadActor(int entity)
    {
        var role = _entities.Get<DataRoleComponent>(entity);
        var actor = _entities.Get<DataActorComponent>(entity);
        return new DataActorState(role.RoleId, role.Gender, _entities.Get<DataPositionComponent>(entity).Value,
            actor.Motion, actor.Facing, actor.MotionVersion, role.HeadId, actor.MainHandId, actor.OffHandId)
        { AttackDurationMs = actor.AttackDurationMs, AttackElapsedMs = actor.AttackElapsedMs };
    }

    /// <summary>追加由输入或会话层转换的 Action。</summary>
    public void Enqueue(WorldAction action) => _actions.Enqueue(action);

    /// <summary>由 Godot 固定帧处理 Action 后按顺序运行逻辑系统。</summary>
    public void Tick(double delta)
    {
        if (HasWorld) Time.Now += delta * 1000;
        while (_actions.TryDequeue(out var action)) Apply(action);
        if (!HasRole) return;
        foreach (var system in _systems) system.Update(this, delta);
    }

    /// <summary>输入阶段即时处理 Action、走近及动作，不推进时间或自动起手。</summary>
    internal void FlushActions()
    {
        while (_actions.TryDequeue(out var action)) Apply(action);
        if (!HasRole) return;
        _approach.Update(this, 0);
        _motion.Update(this, 0);
    }

    /// <summary>按类型处理初始化、移动和权威同步消息。</summary>
    private void Apply(WorldAction action)
    {
        switch (action)
        {
            case InitializeWorldAction world:
                _entities.Clear();
                _mapEntities.Clear();
                _remoteEntities.Clear();
                MapObjects = null;
                _roleEntity = 0;
                _lastConfirmedAction = 0;
                _lastProfileRevision = 0;
                _lastPositionRevision = 0;
                ResetCombat();
                _worldEntity = _entities.Create(world.Map, new DataWorldTimeComponent());
                break;
            case SyncMapObjectsAction syncMap:
                SyncMapObjects(syncMap.Snapshot);
                break;
            case InitializeRoleAction role:
                if (!HasWorld) throw new InvalidOperationException("必须先初始化世界实体");
                _lastProfileRevision = 0;
                _lastPositionRevision = 0;
                _lastConfirmedAction = 0;
                _roleEntity = _entities.Create(new DataRoleComponent(role.Role.RoleId, role.Role.Gender, role.Role.HeadId),
                    new DataPositionComponent(role.Role.Position), new DataMovementComponent(), new DataActorComponent(),
                    new DataAttributesComponent(EnvService.DefaultMovementSpeed), new DataCombatIntentComponent(),
                    GetAttack(role.Role.RoleId), new DataCombatCooldownComponent());
                break;
            case SyncMovementAction movement:
                if (!HasRole) return;
                _entities.Get<DataPositionComponent>(_roleEntity).Value = movement.Position;
                _entities.Get<DataMovementComponent>(_roleEntity).Target = movement.Target;
                _entities.Get<DataPositionComponent>(_roleEntity).AuthorityValue = movement.Position;
                if (movement.Target is null)
                {
                    _entities.Get<DataMovementComponent>(_roleEntity).Moving = false;
                    _entities.Get<DataActorComponent>(_roleEntity).RequestedMotion = null;
                }
                break;
            case SyncRemoteActorsAction remotes:
                var ids = remotes.Actors.Select(item => item.RoleId).ToHashSet();
                foreach (var pair in _remoteEntities.ToArray()) if (!ids.Contains(pair.Key))
                { _entities.Remove(pair.Value); _remoteEntities.Remove(pair.Key); _attackStates.Remove(pair.Key); }
                foreach (var remote in remotes.Actors)
                {
                    if (!_remoteEntities.TryGetValue(remote.RoleId, out var entity))
                        _remoteEntities[remote.RoleId] = entity = _entities.Create(new DataRoleComponent(remote.RoleId, remote.Gender, remote.HeadId),
                            new DataPositionComponent(remote.Position), new DataActorComponent(), new DataMovementComponent(), GetAttack(remote.RoleId));
                    _entities.Get<DataPositionComponent>(entity).Value = remote.Position;
                    var remoteRole = _entities.Get<DataRoleComponent>(entity); remoteRole.Gender = remote.Gender; remoteRole.HeadId = remote.HeadId;
                    var remoteActor = _entities.Get<DataActorComponent>(entity);
                    remoteActor.RequestedMotion = remote.Motion; remoteActor.RequestedMotionVersion = remote.MotionVersion;
                    _entities.Get<DataMovementComponent>(entity).Facing = remote.Facing;
                    remoteActor.MainHandId = remote.MainHandId; remoteActor.OffHandId = remote.OffHandId;
                }
                break;
            case MoveAction move:
                if (!HasRole) return;
                CancelCombat(stopMovement: false);
                _nextActionId = Math.Max(_nextActionId, move.ActionId);
                var map = _entities.Get<DataMapComponent>(_worldEntity);
                _entities.Get<DataMovementComponent>(_roleEntity).Target = MapUtils.Clamp(map, move.Destination);
                _entities.Get<DataActorComponent>(_roleEntity).RequestedMotion = null;
                break;
            case SetRoleMotionAction motion:
                if (!HasRole || motion.RoleId != _entities.Get<DataRoleComponent>(_roleEntity).RoleId) return;
                if (!Enum.IsDefined(motion.Motion) || motion.Motion is RoleMotion.Run or RoleMotion.Stand)
                    throw new ArgumentException("站立与跑步由移动系统维护，动作请求必须使用有效的非移动动作");
                _entities.Get<DataMovementComponent>(_roleEntity).Target = null;
                var actor = _entities.Get<DataActorComponent>(_roleEntity);
                actor.RequestedMotion = motion.Motion;
                actor.RequestedMotionVersion++;
                _entities.Get<DataMovementComponent>(_roleEntity).Moving = false;
                break;
            case SyncEquipmentAction equipment:
                if (!HasRole || equipment.RoleId != _entities.Get<DataRoleComponent>(_roleEntity).RoleId) return;
                var equippedActor = _entities.Get<DataActorComponent>(_roleEntity);
                if (equipment.Revision <= equippedActor.EquipmentRevision) return;
                if (equippedActor.EquipmentRevision >= 0) CancelCombat(stopMovement: true);
                equippedActor.EquipmentRevision = equipment.Revision;
                equippedActor.MainHandId = equipment.MainHandId;
                equippedActor.OffHandId = equipment.OffHandId;
                break;
            case SyncRoleAction sync:
                if (!HasRole || sync.State.RoleId != _entities.Get<DataRoleComponent>(_roleEntity).RoleId) return;
                if (sync.State.ProfileRevision > _lastProfileRevision)
                {
                    _lastProfileRevision = sync.State.ProfileRevision;
                    _entities.Get<DataRoleComponent>(_roleEntity).Gender = sync.State.Gender;
                    _entities.Get<DataRoleComponent>(_roleEntity).HeadId = sync.State.HeadId;
                }
                if (sync.State.PositionRevision <= _lastPositionRevision ||
                    sync.State.AcknowledgedAction < _lastConfirmedAction) return;
                _lastPositionRevision = sync.State.PositionRevision;
                _lastConfirmedAction = sync.State.AcknowledgedAction;
                _entities.Get<DataPositionComponent>(_roleEntity).Value =
                    MapUtils.Clamp(_entities.Get<DataMapComponent>(_worldEntity), sync.State.Position);
                break;
            default:
                ApplyCombat(action);
                break;
        }
    }

    /// <summary>按权威 AOI 差集维护实体，并在完成后发布组件读模型。</summary>
    private void SyncMapObjects(DataMapAoiSnapshotPbData? snapshot)
    {
        if (!HasWorld) return;
        foreach (var (id, entity) in _mapEntities.ToArray())
            if (snapshot is null || !snapshot.Objects.ContainsKey(id))
            {
                _entities.Remove(entity);
                _mapEntities.Remove(id);
            }
        if (snapshot is null) { MapObjects = null; return; }
        foreach (var value in snapshot.Objects.Values)
        {
            if (_deadObjects.ContainsKey(value.ObjectId)) continue;
            if (_mapEntities.TryGetValue(value.ObjectId, out var existing) &&
                _entities.Get<DataMapObjectComponent>(existing).Revision > value.Revision) continue;
            if (value.MonsterTypeId is not null && value.MaxHealth > 0 && value.Health == 0)
            {
                RemoveDeadMonster(value.ObjectId, value.Revision);
                continue;
            }
            if (!_mapEntities.TryGetValue(value.ObjectId, out var entity))
            {
                entity = _entities.Create(new DataPositionComponent(new Vector2(value.X, value.Y)),
                    new DataMapObjectComponent(value));
                _mapEntities.Add(value.ObjectId, entity);
                if (value.MonsterTypeId is not null) _entities.Set(entity, new DataHealthComponent(value.Health, value.MaxHealth));
            }
            else
            {
                SyncMapValue(entity, value);
            }
        }
        PublishMapObjects(snapshot.Sequence);
    }

    /// <summary>只从组件复制地图读模型，生命与位置采用相同已接纳版本。</summary>
    private void PublishMapObjects(long sequence)
    {
        var states = new Dictionary<Guid, DataWorldObjectState>();
        foreach (var (id, entity) in _mapEntities)
        {
            var position = _entities.Get<DataPositionComponent>(entity).Value;
            var item = _entities.Get<DataMapObjectComponent>(entity);
            var health = _entities.Find<DataHealthComponent>(entity);
            states.Add(id, new(entity, id, item.Revision, position.X, position.Y,
                item.MonsterTypeId, item.ItemId, item.Count) { Health = health?.Current ?? 0, MaxHealth = health?.Maximum ?? 0 });
        }
        MapObjects = new(sequence, states.ToFrozenDictionary());
    }
}
