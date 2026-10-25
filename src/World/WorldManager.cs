using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using Godot;
using Rpg.Const;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Systems;
using Rpg.World.Utils;
using Rpg.Session.Data;

namespace Rpg.World;

/// <summary>管理 Action 唯一入口、ECS 实体和固定帧系统顺序。</summary>
internal sealed class WorldManager
{
    /// <summary>待处理的输入和权威同步消息。</summary>
    private readonly Queue<WorldAction> _actions = new();
    /// <summary>该世界的实体组件仓库。</summary>
    private readonly EntityStore _entities = new();
    /// <summary>服务器对象 GUID 对应当前世界的整数实体 ID。</summary>
    private readonly Dictionary<Guid, int> _mapEntities = new();
    /// <summary>由当前 ECS 组件生成的只读对象集合。</summary>
    public DataWorldObjectsState? MapObjects { get; private set; }
    /// <summary>固定运行顺序的逻辑系统。</summary>
    private readonly IWorldSystem[] _systems = [new MovementSystem()];
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
    public DataActorState? ActorState
    {
        get
        {
            if (!HasRole) return null;
            var role = _entities.Get<DataRoleComponent>(_roleEntity);
            var actor = _entities.Get<DataActorComponent>(_roleEntity);
            return new DataActorState(role.RoleId, role.Gender, RolePosition,
                actor.Motion, actor.Facing, actor.MotionVersion, role.HeadId);
        }
    }

    /// <summary>追加由输入或会话层转换的 Action。</summary>
    public void Enqueue(WorldAction action) => _actions.Enqueue(action);

    /// <summary>由 Godot 固定帧处理 Action 后按顺序运行逻辑系统。</summary>
    public void Tick(double delta)
    {
        while (_actions.TryDequeue(out var action)) Apply(action);
        if (!HasRole) return;
        foreach (var system in _systems) system.Update(_entities, _roleEntity, delta);
    }

    /// <summary>按类型处理初始化、移动和权威同步消息。</summary>
    private void Apply(WorldAction action)
    {
        switch (action)
        {
            case InitializeWorldAction world:
                _entities.Clear();
                _mapEntities.Clear();
                MapObjects = null;
                _roleEntity = 0;
                _lastConfirmedAction = 0;
                _lastProfileRevision = 0;
                _lastPositionRevision = 0;
                _worldEntity = _entities.Create(world.Map);
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
                    new DataPositionComponent(role.Role.Position), new DataMovementComponent(), new DataActorComponent());
                break;
            case MoveAction move:
                if (!HasRole) return;
                var map = _entities.Get<DataMapComponent>(_worldEntity);
                _entities.Get<DataMovementComponent>(_roleEntity).Target = MapUtils.Clamp(map, move.Destination);
                break;
            case SetRoleMotionAction motion:
                if (!HasRole || motion.RoleId != _entities.Get<DataRoleComponent>(_roleEntity).RoleId) return;
                if (!Enum.IsDefined(motion.Motion) || motion.Motion is RoleMotion.Run or RoleMotion.Stand)
                    throw new ArgumentException("站立与跑步由移动系统维护，动作请求必须使用有效的非移动动作");
                _entities.Get<DataMovementComponent>(_roleEntity).Target = null;
                var actor = _entities.Get<DataActorComponent>(_roleEntity);
                actor.Motion = motion.Motion;
                actor.MotionVersion++;
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
            if (!_mapEntities.TryGetValue(value.ObjectId, out var entity))
            {
                entity = _entities.Create(new DataPositionComponent(new Vector2(value.X, value.Y)),
                    new DataMapObjectComponent(value));
                _mapEntities.Add(value.ObjectId, entity);
            }
            else
            {
                _entities.Get<DataPositionComponent>(entity).Value = new Vector2(value.X, value.Y);
                var component = _entities.Get<DataMapObjectComponent>(entity);
                component.Revision = value.Revision;
                component.MonsterTypeId = value.MonsterTypeId;
                component.ItemId = value.ItemId;
                component.Count = value.Count;
            }
        }
        var states = new Dictionary<Guid, DataWorldObjectState>();
        foreach (var (id, entity) in _mapEntities)
        {
            var position = _entities.Get<DataPositionComponent>(entity).Value;
            var item = _entities.Get<DataMapObjectComponent>(entity);
            states.Add(id, new(entity, id, item.Revision, position.X, position.Y,
                item.MonsterTypeId, item.ItemId, item.Count));
        }
        MapObjects = new(snapshot.Sequence, states.ToFrozenDictionary());
    }
}
