using System;

namespace Rpg.Utils;

/// <summary>客户端与服务器共用的普通攻击规则。</summary>
public static class CombatUtil
{
    /// <summary>按每秒次数计算完整动作间隔，不使用整数除法。</summary>
    public static double Duration(double speed) => double.IsFinite(speed) && speed > 0 ? 1000d / speed : throw new ArgumentOutOfRangeException(nameof(speed));
    /// <summary>闭区间随机整数，包括上限；允许上下限相同。</summary>
    public static int Roll(int min, int max, Random random) => min >= 0 && max >= min ? (int)random.NextInt64(min, (long)max + 1) : throw new ArgumentOutOfRangeException(nameof(min));
}
