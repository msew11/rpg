using Godot;
using System;

/// <summary>定义可附加到 ECS 实体的数据组件基类。</summary>
[GlobalClass]
public abstract partial class EcsComponent : RefCounted
{
    /// <summary>返回需要落地的字符串；空值表示该组件无持久数据。</summary>
    public virtual string? ToPersistedString() => null;

    /// <summary>从落地字符串恢复运行时状态，未支持的组件应覆写此方法。</summary>
    public virtual void FromPersistedString(string value) =>
        throw new NotSupportedException($"{GetType().Name} does not support persisted data.");
}
