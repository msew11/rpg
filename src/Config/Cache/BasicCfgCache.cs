using System.IO;
using System.Linq;
using Rpg.Const;
using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>加载基础单例配置并检查容量计算。</summary>
[CfgFile("basic.json")]
public sealed class BasicCfgCache : SingleCfgCache<BasicCfg>
{
    /// <summary>由配置服务创建并拥有。</summary>
    internal BasicCfgCache() { }

    /// <summary>检查基础容量的跨字段溢出。</summary>
    protected override void Validate(BasicCfg value)
    {
        value.GetBagCapacity(0);
        foreach (var gender in new[] { RoleGender.Male, RoleGender.Female })
        foreach (var motion in new[] { RoleMotion.Attack1, RoleMotion.Attack2, RoleMotion.Attack3 })
            if (value.AttackTimings.Count(row => row.Gender == gender && row.Motion == motion &&
                double.IsFinite(row.SourceDurationMs) && row.SourceDurationMs > 0 && row.HitRatio > 0 && row.HitRatio < 1) != 1)
                throw new InvalidDataException("攻击时序缺失、重复或无效");
    }

    /// <summary>地图表全部加载后检查初始地图引用。</summary>
    protected override void Validate(ConfigService config)
    {
        if (!config.Map.Items.ContainsKey(Value.InitialMapId))
            throw new InvalidDataException("basic.json 的 initialMapId 不存在于 map.json");
    }
}
