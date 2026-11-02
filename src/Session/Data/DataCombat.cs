using Godot;
using Rpg.Const;

namespace Rpg.Session.Data;

/// <summary>单次权威攻击的不可变动作快照。</summary>
public sealed record DataAttack(long RoleId, ulong AttackId, System.Guid TargetId, long StartMs,
    double DurationMs, double HitTimeMs, RoleMotion Motion, Vector2 TargetPosition);

/// <summary>起手结果，用于对应预测的接受或撤销。</summary>
public sealed record DataAttackReply(ulong AttackId, bool Accepted, string Reason, DataAttack? Attack);

/// <summary>独立于已移除怪物节点的权威伤害表现。</summary>
public sealed record DataCombatHit(long RoleId, ulong AttackId, int Damage, Vector2 Position, bool Dead);
