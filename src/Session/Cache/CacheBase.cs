using Rpg.Services;
using System;
using System.Collections.Generic;

namespace Rpg.Session.Cache;

/// <summary>统一管理功能缓存的优先订阅和释放生命周期。</summary>
public abstract class CacheBase : IDisposable
{
    /// <summary>当前缓存拥有的全部协议订阅句柄。</summary>
    private readonly List<IDisposable> _subscriptions = [];
    /// <summary>是否已释放缓存，防止重复清理或再次订阅。</summary>
    private bool _disposed;
    /// <summary>当前缓存所属的事件域，构造后不再替换。</summary>
    private readonly EventService _events;

    /// <summary>公开缓存默认使用当前完整注册中的事件域。</summary>
    protected CacheBase() : this(GameServices.Events) { }

    /// <summary>内部组装显式绑定事件域，不建立额外全局状态。</summary>
    internal CacheBase(EventService events)
    {
        ArgumentNullException.ThrowIfNull(events);
        ObjectDisposedException.ThrowIf(events.IsDisposed, events);
        _events = events;
    }

    /// <summary>在缓存阶段订阅协议，并自动托管订阅句柄。</summary>
    protected void Subscribe<T>(Action<T> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _subscriptions.Add(_events.Subscribe(handler, EventPhase.Cache));
    }

    /// <summary>幂等释放全部订阅，再执行子类额外清理。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
        OnDispose();
    }

    /// <summary>供子类释放订阅之外的资源，基类确保只调用一次。</summary>
    protected virtual void OnDispose()
    {
    }
}
