using System;
using Rpg.Server;

namespace Rpg.Net;

/// <summary>在同进程内连接独立 MockServer，边界仅允许 protobuf 字节。</summary>
public sealed class MockTransport : INetTransport
{
    /// <summary>此传输连接的本地模拟服务端。</summary>
    private readonly MockServer _server;
    /// <summary>最近一次重置的代次。</summary>
    private long _generation;
    /// <summary>是否已断开此传输。</summary>
    private bool _disposed;
    /// <summary>交付收到的服务器信封字节。</summary>
    public event Action<byte[]>? Received;

    /// <summary>连接服务端字节输出。</summary>
    public MockTransport(MockServer server)
    {
        _server = server;
        _server.Outgoing += OnOutgoing;
    }

    /// <summary>复制请求字节并交由服务端解析。</summary>
    public void Send(byte[] packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _server.Receive((byte[])packet.Clone());
    }

    /// <summary>推进服务端模拟时间。</summary>
    public void Tick(double delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _server.Tick(delta);
    }

    /// <summary>清理服务端角色工作，切换会话代次。</summary>
    public void ResetSession(long generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generation = generation;
        _server.ResetSession(generation);
    }

    /// <summary>断开输出并结束待处理的角色模拟。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _server.Flush();
        _disposed = true;
        _server.Outgoing -= OnOutgoing;
        _server.ResetSession(_generation + 1);
        Received = null;
    }

    /// <summary>向客户端交付独立字节副本。</summary>
    private void OnOutgoing(byte[] packet) => Received?.Invoke((byte[])packet.Clone());
}
