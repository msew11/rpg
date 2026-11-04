using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Session.Data;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Utils;

namespace Rpg.World;

/// <summary>世界战斗 Action、系统数据访问与有序出站事件。</summary>
internal sealed partial class WorldManager
{
    /// <summary>尚未由协调层取走的出站意图。</summary>
    private readonly Queue<DataWorldEvent> _events = new();
    /// <summary>按角色保存攻击组件，允许起手通知先于远端实体到达。</summary>
    private readonly Dictionary<long, DataAttackComponent> _attackStates = new();
    /// <summary>当前控制世界内共享的移动、攻击和取消编号。</summary>
    private long _nextActionId;
    /// <summary>只供世界内部系统读取组件的仓库。</summary>
    internal EntityStore Entities => _entities;
    /// <summary>当前受控角色的实体身份。</summary>
    internal int RoleEntity => _roleEntity;
    /// <summary>统一世界时钟与控制状态。</summary>
    internal DataWorldTimeComponent Time => _entities.Get<DataWorldTimeComponent>(_worldEntity);
    /// <summary>需要合成动作的本人及可见远端实体。</summary>
    internal IEnumerable<int> ActorEntities => _remoteEntities.Values.Prepend(_roleEntity);
    /// <summary>持续攻击目标的只读值。</summary>
    internal Guid? AttackTarget => HasRole ? _entities.Get<DataCombatIntentComponent>(_roleEntity).Target : null;
    /// <summary>当前是否存在需要取消的意图或单次攻击。</summary>
    internal bool HasCombat => AttackTarget is not null || HasRole && _entities.Get<DataAttackComponent>(_roleEntity).Current is not null;

    /// <summary>分配全世界统一递增的输入及出站编号。</summary>
    internal long NextActionId() => checked(++_nextActionId);

    /// <summary>系统只追加意图，网络由协调层负责。</summary>
    internal void Emit(DataWorldEvent value) => _events.Enqueue(value);

    /// <summary>按产生顺序一次取走出站事件。</summary>
    internal DataWorldEvent[] TakeEvents()
    {
        var result = _events.ToArray();
        _events.Clear();
        return result;
    }

    /// <summary>查找当前 AOI 中仍有效的怪物目标。</summary>
    internal bool TryGetMonster(Guid id, out DataWorldObjectState target)
    {
        target = null!;
        return MapObjects is { } objects && objects.Objects.TryGetValue(id, out target!) && target.MonsterTypeId is not null;
    }

    /// <summary>复用已有移动预测，输出供协调层发送的请求。</summary>
    internal void RequestMovement(Vector2 destination, bool force)
    {
        var action = new MoveAction(NextActionId(), MapUtils.Clamp(_entities.Get<DataMapComponent>(_worldEntity), destination));
        _entities.Get<DataMovementComponent>(_roleEntity).Target = action.Destination;
        _entities.Get<DataActorComponent>(_roleEntity).RequestedMotion = null;
        Emit(new DataMoveRequested(action, force));
    }

    /// <summary>取消循环及当前动作，保留冷却和取消编号以拒绝迟到确认。</summary>
    internal void CancelCombat(bool stopMovement)
    {
        if (!HasRole) return;
        var intent = _entities.Get<DataCombatIntentComponent>(_roleEntity);
        var attack = _entities.Get<DataAttackComponent>(_roleEntity);
        if (intent.Target is null && attack.Current is null) return;
        var wasChasing = intent.Target is not null && RoleTarget is not null;
        intent.Target = null;
        intent.ChaseDestination = null;
        if (attack.Current is { } current) attack.CancelledThrough = Math.Max(attack.CancelledThrough, current.AttackId);
        attack.Current = null;
        attack.Pending = false;
        attack.Confirmed = false;
        if (Time.CanAct) Emit(new DataAttackCancelRequested(checked((ulong)NextActionId())));
        if (stopMovement && wasChasing)
        {
            _entities.Get<DataMovementComponent>(_roleEntity).Moving = false;
            if (Time.CanAct) RequestMovement(RolePosition, force: true);
            else _entities.Get<DataMovementComponent>(_roleEntity).Target = null;
        }
    }

    /// <summary>取得角色的共享攻击数据，实体建立时挂入同一份组件。</summary>
    private DataAttackComponent GetAttack(long roleId)
    {
        if (!_attackStates.TryGetValue(roleId, out var attack)) _attackStates[roleId] = attack = new DataAttackComponent();
        return attack;
    }

    /// <summary>接纳世界外转换后的纯数据战斗消息。</summary>
    private void ApplyCombat(WorldAction action)
    {
        switch (action)
        {
            case SyncCombatContextAction context when HasRole:
                Time.Now = Math.Max(Time.Now, context.ServerTimeMs);
                Time.CanAct = context.CanAct;
                _entities.Get<DataPositionComponent>(_roleEntity).AuthorityValue = context.AuthorityPosition;
                var attributes = _entities.Get<DataAttributesComponent>(_roleEntity);
                attributes.MovementSpeed = context.MovementSpeed;
                attributes.AttackRange = context.AttackRange;
                attributes.AttacksPerSecond = context.AttacksPerSecond;
                attributes.AttackMotion = context.AttackMotion;
                attributes.HitRatio = context.HitRatio;
                if (!context.CanAct) CancelCombat(stopMovement: true);
                break;
            case SetCombatIntentAction intent when HasRole && Time.CanAct:
                var combatIntent = _entities.Get<DataCombatIntentComponent>(_roleEntity);
                if (combatIntent.Target == intent.Target || !TryGetMonster(intent.Target, out _)) break;
                CancelCombat(stopMovement: false);
                combatIntent.Target = intent.Target;
                combatIntent.ChaseDestination = null;
                break;
            case CancelCombatAction cancel:
                CancelCombat(cancel.StopMovement);
                break;
            case SyncAttackStartedAction started:
                AcceptAttack(started.Attack);
                break;
            case SyncAttackCancelledAction cancelled:
                var cancelledState = GetAttack(cancelled.RoleId);
                if (OlderAuthority(cancelledState, cancelled.StartMs, cancelled.AttackId)) break;
                cancelledState.AuthorityStart = cancelled.StartMs;
                cancelledState.AuthorityId = cancelled.AttackId;
                cancelledState.AuthorityCancelled = true;
                if (cancelledState.Current is { } active && active.AttackId == cancelled.AttackId &&
                    (active.StartMs == cancelled.StartMs || HasRole && cancelled.RoleId == _entities.Get<DataRoleComponent>(_roleEntity).RoleId))
                { cancelledState.Current = null; cancelledState.Pending = false; cancelledState.Confirmed = false; }
                break;
            case SyncAttackReplyAction response when HasRole:
                var state = _entities.Get<DataAttackComponent>(_roleEntity);
                var reply = response.Reply;
                if (!state.Pending || state.Current is not { } predicted || predicted.AttackId != reply.AttackId) break;
                state.Pending = false;
                if (reply.Accepted && reply.Attack is { } accepted)
                {
                    AcceptAttack(accepted);
                    _entities.Get<DataCombatCooldownComponent>(_roleEntity).NextAttackAt = Math.Max(
                        _entities.Get<DataCombatCooldownComponent>(_roleEntity).NextAttackAt, accepted.StartMs + accepted.DurationMs);
                }
                else
                {
                    state.Current = null;
                    state.Confirmed = false;
                    state.CancelledThrough = Math.Max(state.CancelledThrough, reply.AttackId);
                    _entities.Get<DataCombatCooldownComponent>(_roleEntity).NextAttackAt = Time.Now + 100;
                }
                break;
            case SyncCombatHitAction hit:
                AcceptHit(hit);
                break;
        }
    }

    /// <summary>比较服务器时间与编号，允许远端换控制后编号重新开始。</summary>
    private static bool OlderAuthority(DataAttackComponent state, long start, ulong id)
        => start < state.AuthorityStart || start == state.AuthorityStart && id < state.AuthorityId;

    /// <summary>只接纳较新的有效起手，不让旧确认覆盖新的本人预测。</summary>
    private void AcceptAttack(DataAttack attack)
    {
        var state = GetAttack(attack.RoleId);
        if (OlderAuthority(state, attack.StartMs, attack.AttackId) ||
            state.AuthorityStart == attack.StartMs && state.AuthorityId == attack.AttackId && state.AuthorityCancelled) return;
        state.AuthorityStart = attack.StartMs;
        state.AuthorityId = attack.AttackId;
        state.AuthorityCancelled = false;
        var local = HasRole && attack.RoleId == _entities.Get<DataRoleComponent>(_roleEntity).RoleId;
        if (local && (attack.AttackId <= state.CancelledThrough || state.Current is { } current && current.AttackId > attack.AttackId)) return;
        state.Current = attack;
        state.Confirmed = true;
        if (local)
            _entities.Get<DataCombatCooldownComponent>(_roleEntity).NextAttackAt = Math.Max(
                _entities.Get<DataCombatCooldownComponent>(_roleEntity).NextAttackAt, attack.StartMs + attack.DurationMs);
    }

    /// <summary>进入新世界时清除旧编号、事件、攻击及生命版本。</summary>
    private void ResetCombat()
    {
        _nextActionId = 0;
        _events.Clear();
        _attackStates.Clear();
        _damageNumbers.Clear();
        _hitVersions.Clear();
        _deadObjects.Clear();
    }
}
