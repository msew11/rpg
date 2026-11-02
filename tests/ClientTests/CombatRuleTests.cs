using Rpg.Utils;

namespace ClientTests;

/// <summary>对应 openspec/specs/basic-monster-combat：攻速换算与闭区间随机伤害。</summary>
public sealed class CombatRuleTests
{
    /// <summary>攻速实际值决定完整动作间隔，小数不截断。</summary>
    [Theory]
    [InlineData(1, 1000)]
    [InlineData(1.25, 800)]
    [InlineData(1.5, 666.6666666666666)]
    public void AttackSpeedControlsFullDuration(double speed, double expected) => Assert.Equal(expected, CombatUtil.Duration(speed), 8);
    /// <summary>上下限均可出现，固定区间确定返回，最大整数不溢出。</summary>
    [Fact]
    public void DamageIncludesBothEndpointsAndHandlesFixedRange()
    {
        var random = new Random(17); var samples = Enumerable.Range(0, 200).Select(_ => CombatUtil.Roll(4, 7, random)).ToArray();
        Assert.Contains(4, samples); Assert.Contains(7, samples); Assert.All(samples, value => Assert.InRange(value, 4, 7));
        Assert.Equal(3, CombatUtil.Roll(3, 3, random)); Assert.Equal(int.MaxValue, CombatUtil.Roll(int.MaxValue, int.MaxValue, random));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatUtil.Roll(7, 4, random));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatUtil.Duration(0));
    }
}
