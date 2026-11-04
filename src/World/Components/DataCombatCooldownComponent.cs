namespace Rpg.World.Components;

/// <summary>独立于动作取消和结束的攻击间隔。</summary>
internal sealed class DataCombatCooldownComponent
{
    /// <summary>统一战斗时钟中允许下一次起手的毫秒时间。</summary>
    public double NextAttackAt;

    /// <summary>建立尚未登记攻击间隔的状态。</summary>
    public DataCombatCooldownComponent()
    {
        NextAttackAt = 0;
    }
}
