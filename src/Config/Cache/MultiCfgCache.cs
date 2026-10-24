using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpg.Config.Cache;

/// <summary>将 JSON 根数组按标注主键加载为只读配置映射。</summary>
public abstract class MultiCfgCache<TKey, TCfg> : CfgCache where TKey : notnull where TCfg : class
{
    /// <summary>缓存主键 getter，避免逐行查找反射元数据。</summary>
    private readonly Func<TCfg, TKey> _getKey;
    /// <summary>主键在 JSON 中对应的字段名。</summary>
    private readonly string _keyName;
    /// <summary>成功加载后发布的只读映射。</summary>
    private IReadOnlyDictionary<TKey, TCfg>? _items;
    /// <summary>以主键查询的只读配置表。</summary>
    public IReadOnlyDictionary<TKey, TCfg> Items
    {
        get
        {
            ThrowIfDisposed();
            return _items ?? throw new InvalidOperationException($"配置尚未加载：{FileName}");
        }
    }

    /// <summary>要求恰好一个公开实例主键属性且类型与索引类型相同。</summary>
    protected MultiCfgCache()
    {
        var keys = typeof(TCfg).GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.IsDefined(typeof(CfgKeyAttribute))).ToArray();
        if (keys.Length != 1)
            throw new InvalidDataException($"配置 {FileName} 的 {typeof(TCfg).Name} 必须且只能标注一个 CfgKey 主键");
        var key = keys[0];
        if (key.PropertyType != typeof(TKey) || key.GetMethod is not { IsPublic: true, IsStatic: false } ||
            key.GetIndexParameters().Length != 0 || key.IsDefined(typeof(JsonIgnoreAttribute)))
            throw new InvalidDataException($"配置 {FileName} 的主键 {key.Name} 必须是公开可读实例属性且类型为 {typeof(TKey).Name}");
        _getKey = key.GetMethod.CreateDelegate<Func<TCfg, TKey>>();
        _keyName = key.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? JsonOptions.PropertyNamingPolicy!.ConvertName(key.Name);
    }

    /// <summary>逐行检查主键存在、非空且唯一，整表成功后才发布只读映射。</summary>
    protected sealed override void Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new InvalidDataException("多例配置根必须为数组");
        var items = new Dictionary<TKey, TCfg>();
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("多例配置每行必须为对象");
            if (!element.TryGetProperty(_keyName, out var keyElement) || keyElement.ValueKind == JsonValueKind.Null)
                throw new InvalidDataException($"主键 {_keyName} 缺失或为 null");
            var value = element.Deserialize<TCfg>(JsonOptions) ?? throw new InvalidDataException("多例配置行无法解码");
            var key = _getKey(value);
            if (key is null) throw new InvalidDataException($"主键 {_keyName} 为 null");
            Validate(value);
            if (!items.TryAdd(key, value)) throw new InvalidDataException($"重复的主键 {_keyName}：{key}");
        }
        _items = new ReadOnlyDictionary<TKey, TCfg>(items);
    }

    /// <summary>供具体缓存补充单行约束。</summary>
    protected virtual void Validate(TCfg value) { }

    /// <summary>释放多例配置映射。</summary>
    protected sealed override void Clear() { _items = null; }
}
