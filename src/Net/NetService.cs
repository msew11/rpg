using System;
using System.Collections.Generic;
using System.Diagnostics;
using Google.Protobuf;
using Rpg.Session;
using Rpg.Services;

namespace Rpg.Net;

/// <summary>管理请求队列、protobuf 字节和按类型分发的服务器消息。</summary>
public sealed class NetService : IService
{
    /// <summary>当前使用的可替换传输。</summary>
    private readonly INetTransport _transport;
    /// <summary>发送时已冻结的请求信封。</summary>
    private readonly Queue<byte[]> _outbound = new();
    /// <summary>尚未在固定帧消费的接收字节。</summary>
    private readonly Queue<byte[]> _inbound = new();
    /// <summary>当前网络会话代次。</summary>
    private long _generation = 1;
    /// <summary>是否已释放网络服务。</summary>
    private bool _disposed;
    /// <summary>是否正在推进，避免回调重入固定帧。</summary>
    private bool _ticking;

    /// <summary>贯穿登录与角色会话的事件系统。</summary>
    public EventSystem Events { get; } = new();
    /// <summary>用于会话所有权检查的当前代次。</summary>
    internal long Generation => _generation;
    /// <summary>允许会话在网络服务释放后安全清理。</summary>
    internal bool IsDisposed => _disposed;
    /// <summary>报告编解码、传输和监听错误。</summary>
    public event Action<Exception>? Error;

    /// <summary>绑定传输并建立首个会话代次。</summary>
    public NetService(INetTransport transport)
    {
        _transport = transport;
        _transport.ResetSession(_generation);
        _transport.Received += OnReceived;
        Events.Error += ReportError;
    }

    /// <summary>将请求冻结入队；监听只在后续固定帧执行。</summary>
    public void Send(IMessage request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _outbound.Enqueue(MessageCodec.Encode(request, _generation, true));
    }

    /// <summary>发送当前队列、推进服务端并分发响应，监听中新请求留到下一帧。</summary>
    public void Tick(double delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (_ticking) throw new InvalidOperationException("NetService 不允许重入 Tick");
        _ticking = true;
        try
        {
            var count = _outbound.Count;
            var generation = _generation;
            for (var index = 0; index < count && _outbound.TryDequeue(out var packet); index++)
            {
                try { _transport.Send(packet); }
                catch (Exception error) { ReportError(error); }
                if (_disposed) return;
                if (_generation != generation) break;
            }
            try { _transport.Tick(delta); }
            catch (Exception error) { ReportError(error); }
            while (!_disposed && _inbound.TryDequeue(out var packet))
            {
                try
                {
                    var envelope = MessageCodec.ReadEnvelope(packet);
                    if (envelope.Generation == _generation) Events.Publish(MessageCodec.ReadPayload(envelope, false));
                }
                catch (Exception error) { ReportError(error); }
            }
        }
        finally { _ticking = false; }
    }

    /// <summary>丢弃旧会话请求、延迟消息及当前事件快照。</summary>
    public void ResetSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generation++;
        _outbound.Clear();
        _inbound.Clear();
        Events.ResetPending();
        _transport.ResetSession(_generation);
    }

    /// <summary>释放传输、事件及所有队列。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _transport.Received -= OnReceived;
            _transport.Dispose();
        }
        finally
        {
            _outbound.Clear();
            _inbound.Clear();
            Events.Dispose();
            Error = null;
        }
    }

    /// <summary>接收方拥有独立字节，避免对端修改已入队数据。</summary>
    private void OnReceived(byte[] packet) => _inbound.Enqueue((byte[])packet.Clone());

    /// <summary>隔离错误观察者，保证后续消息仍可处理。</summary>
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
}
