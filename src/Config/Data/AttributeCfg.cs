using System;
using System.IO;
using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>通用属性的范围、生效精度和显示规则。</summary>
public sealed class AttributeCfg
{
    /// <summary>稳定属性编号。</summary>
    [CfgKey]
    public int Id { get; init; }
    /// <summary>显示名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>允许的最小实际值。</summary>
    public double Min { get; init; }
    /// <summary>允许的最大实际值。</summary>
    public double Max { get; init; }
    /// <summary>生效值的小数位数。</summary>
    public int Precision { get; init; }
    /// <summary>界面显示的小数位数。</summary>
    public int DisplayDecimals { get; init; }

    /// <summary>完成一次来源汇总后统一舍入及限制范围。</summary>
    public double Normalize(double value)
    {
        if (!double.IsFinite(value)) throw new InvalidDataException($"属性 {Id} 的数值必须有限");
        var result = Math.Clamp(Math.Round(value, Precision, MidpointRounding.AwayFromZero), Min, Max);
        return result == 0 ? 0 : result;
    }

    /// <summary>投放采用十进制比较，排除二进制表示误差导致的误报。</summary>
    public void ValidateValue(double value)
    {
        if (!double.IsFinite(value) || value < Min || value > Max ||
            Math.Round((decimal)value, Precision, MidpointRounding.AwayFromZero) != (decimal)value)
            throw new InvalidDataException($"属性 {Id} 的值 {value} 超出范围或精度");
    }
}
