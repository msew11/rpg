using Rpg.ServerV2.Connections.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Hosting;

namespace Rpg.Net;

/// <summary>Net 对 V2 服务的字节适配，后台只入服务端队列，接收监听只在 Tick 执行。</summary>
internal sealed class MockServerV2Transport : INetTransport, IServerWriteBackTransport
{
    /// <summary>借用统一装配的服务器服务，传输释放只关闭自己的连接。</summary>
    private readonly MockServerService _server;
    /// <summary>此传输唯一绑定的服务端连接。</summary>
    private readonly DataConnectionId _connection;
    /// <summary>需要在固定帧观察的异步接纳任务。</summary>
    private readonly List<Task> _pending = new();
    /// <summary>客户端与此连接已经同步的代次。</summary>
    private ulong _generation = 1;
    /// <summary>此传输已经结束。</summary>
    private bool _disposed;
    /// <summary>仅在 Net 调用线程交付独立字节。</summary>
    public event Action<byte[]>? Received;
    /// <summary>建立独立匿名连接，生命周期归当前 Net。</summary>
    internal MockServerV2Transport(MockServerService server) { _server = server; _connection = server.OpenConnection(); }
    /// <summary>服务端先复制并入队，不同步等待 Actor 回复。</summary>
    public void Send(byte[] packet)
    { ObjectDisposedException.ThrowIf(_disposed, this); _pending.Add(_server.ReceiveAsync(_connection, packet)); }
    /// <summary>观察后台处理错误并拉取输出，所有回调留在调用方线程。</summary>
    public void Tick(double delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _server.Tick(delta);
        for (var index = _pending.Count - 1; index >= 0; index--)
            if (_pending[index].IsCompleted)
            { var completed = _pending[index]; _pending.RemoveAt(index); completed.GetAwaiter().GetResult(); }
        while (_server.TryReadOutgoing(_connection, out var packet)) Received?.Invoke(packet);
    }
    /// <summary>同步桥接显式保存，等待服务器调用前已接纳请求；失败向 Net 调用方传播。</summary>
    public void FlushServerWrites()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _server.FlushAsync().GetAwaiter().GetResult();
    }
    /// <summary>初次同步不推进，后续只接受紧邻的单调递增代次。</summary>
    public void ResetSession(long generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (generation > 0 && (ulong)generation == _generation) return;
        if (generation <= 0 || (ulong)generation != checked(_generation + 1)) throw new ArgumentOutOfRangeException(nameof(generation));
        _generation = _server.ResetSession(_connection);
    }
    /// <summary>使此连接失效并排空其任务；服务本身由整体所有者释放。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _server.CloseConnectionAsync(_connection).GetAwaiter().GetResult(); }
        finally
        {
            foreach (var task in _pending) _ = task.Exception;
            _pending.Clear();
            Received = null;
        }
    }
}
