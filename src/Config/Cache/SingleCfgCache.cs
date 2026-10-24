using System;
using System.IO;
using System.Text.Json;

namespace Rpg.Config.Cache;

/// <summary>将 JSON 根对象加载为单个类型化配置。</summary>
public abstract class SingleCfgCache<TCfg> : CfgCache where TCfg : class
{
    /// <summary>最近一次成功加载的配置对象。</summary>
    private TCfg? _value;
    /// <summary>完整单例配置，加载前或释放后不可读取。</summary>
    public TCfg Value
    {
        get
        {
            ThrowIfDisposed();
            return _value ?? throw new InvalidOperationException($"配置尚未加载：{FileName}");
        }
    }

    /// <summary>只接受根对象，完成业务校验后再发布配置。</summary>
    protected sealed override void Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("单例配置根必须为对象");
        var value = root.Deserialize<TCfg>(JsonOptions) ?? throw new InvalidDataException("单例配置无法解码");
        Validate(value);
        _value = value;
    }

    /// <summary>供具体缓存补充跨字段约束。</summary>
    protected virtual void Validate(TCfg value) { }

    /// <summary>释放单例配置引用。</summary>
    protected sealed override void Clear() { _value = null; }
}
