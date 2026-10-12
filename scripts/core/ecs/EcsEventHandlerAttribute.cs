using System;

/// <summary>声明系统方法订阅的 ECS 事件类型。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EcsEventHandlerAttribute(Type eventType) : Attribute
{
    /// <summary>该处理方法接收的精确事件类型。</summary>
    public Type EventType { get; } = eventType;
}
