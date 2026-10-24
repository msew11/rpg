using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Rpg.Services;

/// <summary>按职责固定消息分发顺序。</summary>
public enum EventPhase
{
    /// <summary>先维护所有功能缓存。</summary>
    Cache,
    /// <summary>缓存完成后通知 UI 和世界协调器。</summary>
    Normal
}

/// <summary>提供类型监听、缓存优先和排队重入的单线程事件分发。</summary>
public sealed class EventService : IService
{
    /// <summary>仅由 GameServices 建立应用事件域。</summary>
    internal EventService() { }

    /// <summary>各具体消息类型的监听集合。</summary>
    private readonly Dictionary<Type, List<Subscription>> _listeners = new();
    /// <summary>等待完整分发的消息。</summary>
    private readonly Queue<object> _pending = new();
    /// <summary>是否正在消费消息，防止嵌套分发。</summary>
    private bool _dispatching;
    /// <summary>用于使会话重置前的分发快照失效。</summary>
    private long _generation;
    /// <summary>是否已经释放事件系统。</summary>
    private bool _disposed;

    /// <summary>供服务入口判断生命周期是否已结束。</summary>
    internal bool IsDisposed => _disposed;

    /// <summary>报告监听异常；错误监听自身的异常仅记录诊断。</summary>
    public event Action<Exception>? Error;

    /// <summary>按消息具体类型注册监听，并返回幂等释放句柄。</summary>
    public IDisposable Subscribe<T>(Action<T> handler, EventPhase phase = EventPhase.Normal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(handler);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        var type = typeof(T);
        if (!_listeners.TryGetValue(type, out var listeners)) _listeners[type] = listeners = [];
        var subscription = new Subscription(message => handler((T)message), phase, listeners);
        listeners.Add(subscription);
        return subscription;
    }

    /// <summary>发布消息；嵌套发布等待当前消息两个阶段全部结束。</summary>
    public void Publish(object message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        _pending.Enqueue(message);
        if (_dispatching) return;
        _dispatching = true;
        try
        {
            while (_pending.TryDequeue(out var next)) Dispatch(next);
        }
        finally { _dispatching = false; }
    }

    /// <summary>清理旧会话消息并中止当前快照，保留仍有效的常驻监听。</summary>
    public void ResetPending()
    {
        _generation++;
        _pending.Clear();
    }

    /// <summary>清理所有监听及待处理消息。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ResetPending();
        foreach (var listener in _listeners.Values.SelectMany(list => list).ToArray()) listener.Dispose();
        _listeners.Clear();
        Error = null;
    }

    /// <summary>在单个监听快照上依次处理缓存阶段与普通阶段。</summary>
    private void Dispatch(object message)
    {
        if (!_listeners.TryGetValue(message.GetType(), out var listeners)) return;
        var snapshot = listeners.ToArray();
        var generation = _generation;
        var cacheFailed = false;
        foreach (var phase in new[] { EventPhase.Cache, EventPhase.Normal })
        {
            if (phase == EventPhase.Normal && cacheFailed) return;
            foreach (var listener in snapshot)
            {
                if (generation != _generation) return;
                if (!listener.Active || listener.Phase != phase) continue;
                try { listener.Handler(message); }
                catch (Exception error)
                {
                    cacheFailed |= phase == EventPhase.Cache;
                    ReportError(error);
                }
            }
        }
    }

    /// <summary>隔离错误观察者，避免破坏业务消息队列。</summary>
    private void ReportError(Exception error)
    {
        Trace.WriteLine(error);
        if (Error is null) return;
        foreach (Action<Exception> handler in Error.GetInvocationList())
        {
            try { handler(error); }
            catch (Exception observerError) { Trace.WriteLine(observerError); }
        }
    }

    /// <summary>保存一个监听和所在集合，释放时立即移除。</summary>
    private sealed class Subscription(Action<object> handler, EventPhase phase, List<Subscription> owner) : IDisposable
    {
        /// <summary>监听执行委托。</summary>
        public Action<object> Handler { get; } = handler;
        /// <summary>监听的固定执行阶段。</summary>
        public EventPhase Phase { get; } = phase;
        /// <summary>是否仍可执行。</summary>
        public bool Active { get; private set; } = true;
        /// <summary>幂等取消订阅，并使已获取的快照跳过此监听。</summary>
        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            owner.Remove(this);
        }
    }
}
