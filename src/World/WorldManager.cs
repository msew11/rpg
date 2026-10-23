using System;
using System.Collections.Generic;
using Godot;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Systems;

namespace Rpg.World;

/// <summary>管理 Action 唯一入口、ECS 实体和固定帧系统顺序。</summary>
public sealed class WorldManager
{
    /// <summary>待处理的输入和权威同步消息。</summary>
    private readonly Queue<WorldAction> _actions = new();
    /// <summary>该世界的实体组件仓库。</summary>
    private readonly EntityStore _entities = new();
    /// <summary>固定运行顺序的逻辑系统。</summary>
    private readonly IWorldSystem[] _systems = [new MovementSystem()];
    /// <summary>地图世界实体 ID。</summary>
    private int _worldEntity;
    /// <summary>当前角色实体 ID。</summary>
    private int _roleEntity;
    /// <summary>最后应用的权威确认序号。</summary>
    private long _lastConfirmedAction;
    /// <summary>已接纳的角色属性权威版本。</summary>
    private long _lastRoleRevision;

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
                actor.Motion, actor.Facing, actor.MotionVersion);
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
                _roleEntity = 0;
                _lastConfirmedAction = 0;
                _lastRoleRevision = 0;
                _worldEntity = _entities.Create(new DataMapComponent(world.Map));
                break;
            case InitializeRoleAction role:
                if (!HasWorld) throw new InvalidOperationException("必须先初始化世界实体");
                _lastRoleRevision = 0;
                _lastConfirmedAction = 0;
                _roleEntity = _entities.Create(new DataRoleComponent(role.Role.RoleId, role.Role.Gender),
                    new DataPositionComponent(role.Role.Position), new DataMovementComponent(), new DataActorComponent());
                break;
            case MoveAction move:
                if (!HasRole) return;
                var map = _entities.Get<DataMapComponent>(_worldEntity).Map;
                _entities.Get<DataMovementComponent>(_roleEntity).Target = map.Clamp(move.Destination);
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
                if (!HasRole || sync.State.Role.RoleId != _entities.Get<DataRoleComponent>(_roleEntity).RoleId) return;
                if (sync.State.Revision <= _lastRoleRevision) return;
                _lastRoleRevision = sync.State.Revision;
                _entities.Get<DataRoleComponent>(_roleEntity).Gender = sync.State.Role.Gender;
                if (sync.State.AcknowledgedAction <= _lastConfirmedAction) return;
                _lastConfirmedAction = sync.State.AcknowledgedAction;
                _entities.Get<DataPositionComponent>(_roleEntity).Value =
                    _entities.Get<DataMapComponent>(_worldEntity).Map.Clamp(sync.State.Position);
                break;
        }
    }
}
