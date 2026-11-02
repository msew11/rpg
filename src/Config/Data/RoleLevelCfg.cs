using System.Collections.Generic;
using System.Text.Json.Serialization;
using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>指定等级的完整基础属性，不累计此前等级。</summary>
public sealed class RoleLevelCfg
{
    /// <summary>角色等级主键。</summary>
    [CfgKey]
    public int Level { get; init; }
    /// <summary>数字属性编号对应的实际投放值。</summary>
    [JsonConverter(typeof(AttributeMapJsonConverter))]
    public IReadOnlyDictionary<int, double> Attributes { get; init; } = new Dictionary<int, double>();
}
