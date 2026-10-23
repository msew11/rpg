namespace Rpg.Const;

/// <summary>玩家实体的动作语义，不包含帧、资源或播放时间。</summary>
public enum RoleMotion
{
    /// <summary>站立。</summary>
    Stand,
    /// <summary>跑步。</summary>
    Run,
    /// <summary>坐下。</summary>
    Sit,
    /// <summary>死亡姿态。</summary>
    Die,
    /// <summary>第一组攻击动作。</summary>
    Attack1,
    /// <summary>第二组攻击动作。</summary>
    Attack2,
    /// <summary>第三组攻击动作。</summary>
    Attack3,
    /// <summary>施法姿态。</summary>
    Magic,
    /// <summary>第一组准备动作。</summary>
    Prepare1,
    /// <summary>第二组准备动作。</summary>
    Prepare2,
    /// <summary>第三组准备动作。</summary>
    Prepare3,
    /// <summary>受伤姿态。</summary>
    Hurt,
    /// <summary>骑乘姿态。</summary>
    Ride
}
