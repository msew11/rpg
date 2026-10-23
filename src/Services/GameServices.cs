using System;
using Rpg.Net;

namespace Rpg.Services;

/// <summary>提供主线程常驻服务的显式注册、全局访问和生命周期所有权。</summary>
public static class GameServices
{
    /// <summary>当前启动流程拥有的服务注册，释放前禁止替换。</summary>
    private static Registration? _current;

    /// <summary>是否存在尚未释放的网络服务。</summary>
    public static bool IsInitialized => _current is not null && !_current.Net.IsDisposed;

    /// <summary>取得已注册网络；未初始化或被提前释放时明确报错。</summary>
    public static NetService Net
    {
        get
        {
            var net = _current?.Net ?? throw new InvalidOperationException("GameServices 尚未初始化，请先由启动流程注册服务");
            ObjectDisposedException.ThrowIf(net.IsDisposed, net);
            return net;
        }
    }

    /// <summary>成功后接管网络并返回释放句柄；注册失败时网络仍归调用方所有。</summary>
    public static IDisposable Initialize(NetService net)
    {
        ArgumentNullException.ThrowIfNull(net);
        ObjectDisposedException.ThrowIf(net.IsDisposed, net);
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        var registration = new Registration(net);
        _current = registration;
        return registration;
    }

    /// <summary>持有一次启动的服务，防止旧句柄重复释放时影响后续启动。</summary>
    private sealed class Registration : IDisposable
    {
        /// <summary>当前注册接管的网络实例。</summary>
        public NetService Net { get; }
        /// <summary>是否已释放本次注册。</summary>
        private bool _disposed;

        /// <summary>绑定网络实例，由外层登记为当前注册。</summary>
        public Registration(NetService net) => Net = net;

        /// <summary>先撤销全局访问，再幂等释放本次启动的网络和监听。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_current, this)) _current = null;
            Net.Dispose();
        }
    }
}
