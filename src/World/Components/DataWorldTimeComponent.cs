namespace Rpg.World.Components;

/// <summary>世界统一战斗时钟与当前控制是否允许预测。</summary>
internal sealed class DataWorldTimeComponent
{
    /// <summary>估计的服务器毫秒时间，仅向前推进。</summary>
    public double Now;
    /// <summary>当前角色是否具有有效控制及首份世界状态。</summary>
    public bool CanAct;

    /// <summary>建立尚未接纳会话时间的世界时钟。</summary>
    public DataWorldTimeComponent()
    {
        Now = 0;
        CanAct = false;
    }
}
