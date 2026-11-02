using System.Collections.Generic;
using System.IO;

namespace Rpg.Config.Data;

/// <summary>角色与怪物共用的完整属性和点伤契约。</summary>
public static class AttributeRules
{
    /// <summary>现阶段全部有效属性编号；编号4永久停用。</summary>
    public static System.ReadOnlySpan<int> Ids => [1, 2, 3, 5, 6, 7, 8, 9, 10];
    /// <summary>校验完整投放和跨属性上下限。</summary>
    public static void Validate(IReadOnlyDictionary<int, double> values, ConfigService config)
    {
        if (values.Count != config.Attribute.Items.Count) throw new InvalidDataException("必须投放完整属性");
        foreach (var id in Ids)
        {
            if (!values.TryGetValue(id, out var value)) throw new InvalidDataException($"缺少属性 {id}");
            config.Attribute.Items[id].ValidateValue(value);
        }
        if (values[7] > values[8]) throw new InvalidDataException("点伤下限不能超过上限");
    }
}
