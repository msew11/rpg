namespace Rpg.ServerV2.Modules.Role.Data;

/// <summary>不落盘的移动目标、时间、公开版本与动作确认状态。</summary>
public sealed record DataRoleMovement(float? TargetX = null, float? TargetY = null, float Speed = 180,
    long TimeMs = 0, ulong Revision = 1, ulong AcknowledgedAction = 0, bool ProjectionPending = true);
