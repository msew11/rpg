using System;
using System.IO;
using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>属性定义加载与数值可表达范围检查。</summary>
[CfgFile("attribute.json")]
public sealed class AttributeCfgCache : MultiCfgCache<int, AttributeCfg>
{
    /// <summary>仅由配置服务创建。</summary>
    internal AttributeCfgCache() { }

    /// <summary>校验范围、精度以及范围端点的浮点间距。</summary>
    protected override void Validate(AttributeCfg value)
    {
        if (value.Id is < 1 or > 10 or 4 || string.IsNullOrWhiteSpace(value.Name) ||
            !double.IsFinite(value.Min) || !double.IsFinite(value.Max) || value.Min > value.Max ||
            value.Precision is < 0 or > 15 || value.DisplayDecimals < 0 || value.DisplayDecimals > value.Precision)
            throw new InvalidDataException($"属性 {value.Id} 定义无效");
        if (value.Id == 1 && value.Min < 0) throw new InvalidDataException("正常移速范围不能为负数");
        if (value.Id is 7 or 8 && (value.Precision != 0 || value.Min < 0 || value.Max > int.MaxValue))
            throw new InvalidDataException("点伤必须为非负整数");
        if (value.Id is 9 or 10 && value.Min <= 0) throw new InvalidDataException("范围与攻速必须为正数");
        var magnitude = Math.Max(Math.Abs(value.Min), Math.Abs(value.Max));
        if (magnitude > (double)decimal.MaxValue || Math.BitIncrement(magnitude) - magnitude > Math.Pow(10, -value.Precision) / 4)
            throw new InvalidDataException($"属性 {value.Id} 范围不能支持指定精度");
        value.ValidateValue(value.Min); value.ValidateValue(value.Max);
        if (value.Id is 2 or 3 && (value.Precision != 0 || value.Max > int.MaxValue || value.Min < (value.Id == 2 ? 1 : 0)))
            throw new InvalidDataException("生命魔力上限必须满足公开整数状态约束");
    }

    /// <summary>当前属性集必须有全部九项定义。</summary>
    protected override void Validate(ConfigService config)
    {
        foreach (var id in AttributeRules.Ids) if (!Items.ContainsKey(id)) throw new InvalidDataException($"缺少属性定义 {id}");
    }
}
