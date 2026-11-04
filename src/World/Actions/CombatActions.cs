using System;
using Godot;
using Rpg.Const;
using Rpg.Session.Data;

namespace Rpg.World.Actions;

/// <summary>设置持续攻击目标，切换时取消旧攻击但保留冷却。</summary>
internal sealed record SetCombatIntentAction(Guid Target) : WorldAction;

/// <summary>取消持续攻击，按需停止由追击产生的移动。</summary>
internal sealed record CancelCombatAction(bool StopMovement = true) : WorldAction;

/// <summary>同步纯数值属性、权威位置及服务器时间，供世界独立预测。</summary>
internal sealed record SyncCombatContextAction(double ServerTimeMs, Vector2 AuthorityPosition, bool CanAct,
    double MovementSpeed, double AttackRange, double AttacksPerSecond, RoleMotion AttackMotion, double HitRatio) : WorldAction;

/// <summary>接纳本人或远端的权威攻击起手。</summary>
internal sealed record SyncAttackStartedAction(DataAttack Attack) : WorldAction;

/// <summary>接纳带起手身份的权威取消，防止旧取消覆盖新动作。</summary>
internal sealed record SyncAttackCancelledAction(long RoleId, ulong AttackId, long StartMs) : WorldAction;

/// <summary>确认或撤销对应本人预测。</summary>
internal sealed record SyncAttackReplyAction(DataAttackReply Reply) : WorldAction;

/// <summary>接纳权威命中、目标生命与可独立消费的伤害表现。</summary>
internal sealed record SyncCombatHitAction(DataCombatHit Hit, DataMapObjectPbData Target, long ServerTimeMs) : WorldAction;
