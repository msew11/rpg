using System;
using System.IO;
using System.Linq;
using Rpg.Const;

namespace Rpg.Config.Data;

/// <summary>单例基础参数，背包容量由配置与角色增加行数共同决定。</summary>
public sealed class BasicCfg
{
    /// <summary>新角色进入的地图，必须存在于地图表。</summary>
    public int InitialMapId { get; init; } = 1;
    /// <summary>背包每行格子数。</summary>
    public int BagColumns { get; init; }
    /// <summary>背包基础行数。</summary>
    public int BagRows { get; init; }
    /// <summary>全部性别与原生攻击动作的公共时序。</summary>
    public AttackTimingCfg[] AttackTimings { get; init; } = [];
    /// <summary>读取唯一配置，不隐式回退攻击类别。</summary>
    public AttackTimingCfg AttackTiming(RoleGender gender, RoleMotion motion) =>
        AttackTimings.Single(row => row.Gender == gender && row.Motion == motion);

    /// <summary>校验基础值和角色扩容，拒绝容量溢出。</summary>
    public int GetBagCapacity(int extraRows)
    {
        if (BagColumns <= 0 || BagRows <= 0 || extraRows < 0)
            throw new InvalidDataException("背包列数、基础行数或增加行数无效");
        try { return checked(BagColumns * checked(BagRows + extraRows)); }
        catch (OverflowException error) { throw new InvalidDataException("背包容量超出整数范围", error); }
    }
}
