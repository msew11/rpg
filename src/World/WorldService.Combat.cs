using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Rpg.Const;
using Rpg.Session.Data;
using Rpg.World.Actions;

namespace Rpg.World;

/// <summary>将左键目标转换为走近、每次起手和只读攻击动作，不直接依赖节点。</summary>
public sealed partial class WorldService
{
    /// <summary>当前持续攻击目标。</summary>
    private Guid? _attackTarget;
    /// <summary>本地立即播放的起手，等待对应权威结果。</summary>
    private DataAttack? _predictedAttack;
    /// <summary>统一估计服务器时间，持续在世界帧之间推进。</summary>
    private double _combatNow;
    /// <summary>本地下一次允许起手的时间。</summary>
    private double _nextAttackAt;
    /// <summary>对应当前预测的响应是否尚未到达。</summary>
    private bool _attackPending;
    /// <summary>被本地取消的本人动作编号上界，迟到通知不得复活。</summary>
    private ulong _cancelledAttack;
    /// <summary>供验收和表现读取的自动攻击目标。</summary>
    public Guid? AttackTarget => _attackTarget;

    /// <summary>左键开始或切换攻击，范围外沿既有移动路径走近。</summary>
    public void AttackMonster(Guid id)
    {
        ThrowIfDisposed();
        if (!_session.CanMove || _world is null || MapObjects is not { } objects ||
            !objects.Objects.TryGetValue(id, out var target) || target.MonsterTypeId is null) return;
        if (_attackTarget == id) return;
        CancelAttack(stopMovement: false); _attackTarget = id;
        var position = new Vector2(target.X, target.Y); var range = _session.Role.Attributes.Values[9];
        var direction = RolePosition - position;
        var destination = direction.Length() > range ? position + direction.Normalized() * (float)Math.Max(0, range - 0.05) : RolePosition;
        var action = new MoveAction(++_nextActionId, destination);
        _pendingMove = action; _intentions.Clear(); _intentions[action.ActionId] = action;
        SendPendingMove(force: true); _world.Enqueue(action);
    }
    /// <summary>取消自动循环与当前未命中动作，保留已经登记的攻击间隔。</summary>
    public void CancelAttack(bool stopMovement = true)
    {
        if (_attackTarget is null && _predictedAttack is null) return;
        var wasChasing = _attackTarget is not null && RoleTarget is not null;
        _attackTarget = null;
        if (_predictedAttack is { } attack) _cancelledAttack = Math.Max(_cancelledAttack, attack.AttackId);
        _predictedAttack = null; _attackPending = false;
        if (_session.CanMove) _session.CancelAttack(checked((ulong)++_nextActionId));
        if (stopMovement && wasChasing && _world is not null && _session.CanMove)
        {
            var stop = new MoveAction(++_nextActionId, RolePosition);
            _pendingMove = stop; _intentions.Clear(); _intentions[stop.ActionId] = stop;
            SendPendingMove(force: true); _world.Enqueue(stop);
        }
    }
    /// <summary>在ECS推进后按本人与权威位置判断起手，避免预测走近抢先越距。</summary>
    private void TickCombat(double delta)
    {
        if (!_session.HasRole) { ResetCombat(); return; }
        _combatNow = Math.Max(_combatNow + delta * 1000, _session.ServerTimeMs);
        if (_predictedAttack is { } pending && _attackPending && _session.AttackReply is { } reply && reply.AttackId == pending.AttackId)
        {
            _attackPending = false;
            if (reply.Accepted && reply.Attack is { } accepted)
            { _predictedAttack = accepted; _nextAttackAt = Math.Max(_nextAttackAt, accepted.StartMs + accepted.DurationMs); }
            else { _predictedAttack = null; _nextAttackAt = _combatNow + 100; }
        }
        if (_attackTarget is not { } id || _world is null) return;
        if (MapObjects is not { } objects || !objects.Objects.TryGetValue(id, out var target)) { CancelAttack(); return; }
        var goal = new Vector2(target.X, target.Y); var attrs = _session.Role.Attributes.Values;
        if (RolePosition.DistanceTo(goal) > attrs[9] + 0.01 || _session.Role.Position.DistanceTo(goal) > attrs[9] + 0.01 ||
            _attackPending || _combatNow < _nextAttackAt) return;
        var weapon = _session.Equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        var motion = weapon is null ? RoleMotion.Attack1 : _config.Equipment.Items[_config.Item.Items[weapon.ItemId].RelatedId].AttackMotion;
        var duration = Rpg.Utils.CombatUtil.Duration(attrs[10]); var timing = _config.Basic.Value.AttackTiming(_session.Role.Gender, motion);
        var attackId = checked((ulong)++_nextActionId);
        _predictedAttack = new(_session.RoleId, attackId, id, (long)_combatNow, duration, _combatNow + duration * timing.HitRatio, motion, goal);
        _nextAttackAt = _combatNow + duration; _attackPending = true;
        _session.Attack(attackId, id);
    }
    /// <summary>在世界逻辑读模型上呈现对应攻击，不让旧位置帧覆盖动作。</summary>
    private DataActorState CombatPose(DataActorState state)
    {
        _session.Attacks.TryGetValue(state.RoleId, out var attack);
        if (state.RoleId == _session.RoleId)
        {
            if (_predictedAttack is { } local && (attack is null || local.AttackId >= attack.AttackId)) attack = local;
            if (attack is not null && attack.AttackId <= _cancelledAttack) attack = null;
        }
        if (attack is null || _combatNow >= attack.StartMs + attack.DurationMs) return state;
        var facing = attack.TargetPosition - state.Position;
        return state with { Motion = attack.Motion, Facing = facing.LengthSquared() > 0 ? facing.Normalized() : state.Facing,
            MotionVersion = checked((long)attack.AttackId), AttackDurationMs = attack.DurationMs,
            AttackElapsedMs = Math.Max(0, _combatNow - attack.StartMs) };
    }
    /// <summary>取出已确认的伤害，节点可以在怪物移除后继续跳字。</summary>
    public DataCombatHit[] TakeDamageNumbers() => _session.TakeCombatHits();
    /// <summary>离开世界后移除旧攻击和时钟。</summary>
    private void ResetCombat()
    { _attackTarget = null; _predictedAttack = null; _combatNow = 0; _nextAttackAt = 0; _attackPending = false; _cancelledAttack = 0; }
}
