using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpg.Config.Cache;

/// <summary>配置通用属性 map 的规范数字键与有限实际值解析。</summary>
public sealed class AttributeMapJsonConverter : JsonConverter<IReadOnlyDictionary<int, double>>
{
    /// <summary>逐项读取并拒绝重复键，不能让字典反序列化静默覆盖。</summary>
    public override IReadOnlyDictionary<int, double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("属性 map 必须是对象");
        var values = new Dictionary<int, double>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("属性键无效");
            var key = reader.GetString()!;
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0 ||
                key != id.ToString(CultureInfo.InvariantCulture)) throw new JsonException($"属性编号 {key} 非法");
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var value) || !double.IsFinite(value))
                throw new JsonException($"属性 {id} 必须为有限数值");
            if (!values.TryAdd(id, value)) throw new JsonException($"属性编号 {id} 重复");
        }
        if (reader.TokenType != JsonTokenType.EndObject) throw new JsonException("属性 map 不完整");
        return new ReadOnlyDictionary<int, double>(values);
    }

    /// <summary>保持数字字符串键及实际数值的统一配置格式。</summary>
    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<int, double> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value) writer.WriteNumber(pair.Key.ToString(CultureInfo.InvariantCulture), pair.Value);
        writer.WriteEndObject();
    }
}
