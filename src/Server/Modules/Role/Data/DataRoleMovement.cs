namespace Rpg.Server.Modules.Role.Data;

/// <summary>不落盘的移动目标、时间与动作确认状态。</summary>
public sealed record DataRoleMovement(float? TargetX = null, float? TargetY = null, float Speed = 180,
    long TimeMs = 0, ulong AcknowledgedAction = 0);
