using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rpg.ServerV2.Runtime;

/// <summary>每个 Actor 自己的显式处理器表，首次使用后冻结。</summary>
public sealed class ActorHandlers<TContext>
{
    /// <summary>按消息 CLR 类型保存类型化适配器。</summary>
    private readonly Dictionary<Type, IEntry> _entries = new();
    /// <summary>已经交给 Actor 使用，禁止继续修改。</summary>
    private bool _frozen;

    /// <summary>注册请求及回复契约，重复消息类型明确报错。</summary>
    public ActorHandlers<TContext> Register<TRequest, TReply>(IActorHandler<TContext, TRequest, TReply> handler)
        where TRequest : IActorRequest<TReply>
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureMutable();
        _entries.Add(typeof(TRequest), new RequestEntry<TRequest, TReply>(handler));
        return this;
    }

    /// <summary>注册无回复通知契约。</summary>
    public ActorHandlers<TContext> Register<TNotification>(IActorNotificationHandler<TContext, TNotification> handler)
        where TNotification : IActorNotification
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureMutable();
        _entries.Add(typeof(TNotification), new NotificationEntry<TNotification>(handler));
        return this;
    }

    /// <summary>构造 Actor 时冻结，避免后台处理期间注册发生竞争。</summary>
    internal void Freeze() => _frozen = true;
    /// <summary>在所属 Actor 回合中分发，未知消息不改变上下文。</summary>
    internal ValueTask<object?> HandleAsync(TContext context, object message) =>
        _entries.TryGetValue(message.GetType(), out var entry) ? entry.HandleAsync(context, message)
        : throw new InvalidOperationException($"Actor 未注册处理器：{message.GetType().Name}");
    /// <summary>拒绝修改已经绑定 Actor 的注册表。</summary>
    private void EnsureMutable()
    { if (_frozen) throw new InvalidOperationException("Actor 处理器表已经冻结"); }

    /// <summary>注册表内部统一调用契约，不使用反射执行。</summary>
    private interface IEntry
    {
        /// <summary>将对象适配回注册时确定的类型。</summary>
        ValueTask<object?> HandleAsync(TContext context, object message);
    }
    /// <summary>保持请求与回复类型关联的适配器。</summary>
    private sealed class RequestEntry<TRequest, TReply>(IActorHandler<TContext, TRequest, TReply> handler) : IEntry
        where TRequest : IActorRequest<TReply>
    {
        /// <summary>执行注册时确定的请求处理器。</summary>
        public async ValueTask<object?> HandleAsync(TContext context, object message) =>
            await handler.HandleAsync(context, (TRequest)message).ConfigureAwait(false);
    }
    /// <summary>执行通知并返回无业务载荷的完成结果。</summary>
    private sealed class NotificationEntry<TNotification>(IActorNotificationHandler<TContext, TNotification> handler) : IEntry
        where TNotification : IActorNotification
    {
        /// <summary>在同一回合等待通知处理完成。</summary>
        public async ValueTask<object?> HandleAsync(TContext context, object message)
        { await handler.HandleAsync(context, (TNotification)message).ConfigureAwait(false); return null; }
    }
}
