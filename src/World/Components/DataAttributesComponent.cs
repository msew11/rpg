using Rpg.Const;

namespace Rpg.World.Components;

/// <summary>世界预测使用的已解析属性，不引用会话或配置服务。</summary>
internal sealed class DataAttributesComponent
{
    /// <summary>当前实际移动速度，保留小数与 GM 覆盖。</summary>
    public double MovementSpeed;
    /// <summary>按逻辑锚点计算的攻击距离。</summary>
    public double AttackRange;
    /// <summary>每秒允许的攻击次数。</summary>
    public double AttacksPerSecond;
    /// <summary>当前装备解析出的原生攻击动作。</summary>
    public RoleMotion AttackMotion;
    /// <summary>本次动作对应的命中时刻占完整时长的比例。</summary>
    public double HitRatio;

    /// <summary>建立可移动但尚未同步战斗属性的状态。</summary>
    public DataAttributesComponent(double movementSpeed)
    {
        MovementSpeed = movementSpeed;
        AttackRange = 0;
        AttacksPerSecond = 0;
        AttackMotion = RoleMotion.Attack1;
        HitRatio = 0;
    }
}
