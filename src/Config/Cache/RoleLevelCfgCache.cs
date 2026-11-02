using System.IO;
using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>完整等级基础属性加载及关联校验。</summary>
[CfgFile("role_level.json")]
public sealed class RoleLevelCfgCache : MultiCfgCache<int, RoleLevelCfg>
{
    /// <summary>仅由配置服务创建。</summary>
    internal RoleLevelCfgCache() { }
    /// <summary>等级主键必须为正。</summary>
    protected override void Validate(RoleLevelCfg value)
    {
        if (value.Level <= 0) throw new InvalidDataException("等级必须为正整数");
    }
    /// <summary>全表就绪后校验完整属性及对应范围精度。</summary>
    protected override void Validate(ConfigService config)
    {
        if (!Items.ContainsKey(1)) throw new InvalidDataException("缺少 1 级基础属性");
        foreach (var row in Items.Values)
        {
            AttributeRules.Validate(row.Attributes, config);
        }
    }
}
