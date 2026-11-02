using Rpg.Const;

namespace Rpg.Config.Data;

/// <summary>男女原生攻击动作的统一时间基准。</summary>
public sealed class AttackTimingCfg
{
    /// <summary>身体性别。</summary>
    public RoleGender Gender { get; init; }
    /// <summary>原生攻击动作。</summary>
    public RoleMotion Motion { get; init; }
    /// <summary>原速动画总毫秒数，用于整体缩放。</summary>
    public double SourceDurationMs { get; init; }
    /// <summary>命中点占完整动作的比例。</summary>
    public double HitRatio { get; init; }
}
