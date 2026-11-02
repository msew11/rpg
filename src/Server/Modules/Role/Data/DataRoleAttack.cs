using Rpg.Protocol;

namespace Rpg.Server.Modules.Role.Data;

/// <summary>起手后固定的攻击快照，仅属于角色临时工作区。</summary>
internal sealed record DataRoleAttack(AttackStartedNotice Started, int DamageMin, int DamageMax, double Range, bool HitApplied = false);
