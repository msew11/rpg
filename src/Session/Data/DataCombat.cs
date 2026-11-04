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

/// <summary>缓存已验证、等待世界消费的战斗通知。</summary>
internal abstract record DataCombatUpdate;

/// <summary>本人或远端的权威起手数据。</summary>
internal sealed record DataAttackStartedUpdate(DataAttack Attack) : DataCombatUpdate;

/// <summary>带原始起手身份的取消通知。</summary>
internal sealed record DataAttackCancelledUpdate(long RoleId, ulong AttackId, long StartMs) : DataCombatUpdate;

/// <summary>本人对应预测的请求响应。</summary>
internal sealed record DataAttackReplyUpdate(DataAttackReply Reply) : DataCombatUpdate;

/// <summary>携带目标权威版本的确认伤害。</summary>
internal sealed record DataCombatHitUpdate(DataCombatHit Hit, DataMapObjectPbData Target, long ServerTimeMs) : DataCombatUpdate;
