using Rpg.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Google.Protobuf;
using Rpg.Services;
using Rpg.Server;
using Rpg.Server.Data;

namespace Rpg.Net;

/// <summary>管理请求队列、protobuf 字节和按类型分发的服务器消息。</summary>
public sealed class NetService : Service
{
    /// <summary>构造绑定的通信日志，生命周期长于网络。</summary>
    internal LogService Log { get; }
    /// <summary>当前使用的可替换传输。</summary>
    private readonly INetTransport _transport;
    /// <summary>构造时绑定的事件服务，后于网络释放。</summary>
    private readonly EventBusService _eventBus;
    /// <summary>发送时已冻结的请求信封。</summary>
    private readonly Queue<byte[]> _outbound = new();
    /// <summary>尚未在固定帧消费的接收字节。</summary>
    private readonly Queue<byte[]> _inbound = new();
    /// <summary>当前网络会话代次。</summary>
    private long _generation = 1;
    /// <summary>是否正在推进，避免回调重入固定帧。</summary>
    private bool _ticking;

    /// <summary>用于会话所有权检查的当前代次。</summary>
    internal long Generation => _generation;
    /// <summary>报告编解码与传输错误。</summary>
    public event Action<Exception>? Error;

    /// <summary>在服务内部初始化当前阶段的进程内传输。</summary>
    public NetService(EnvService env, EventBusService eventBus, ConfigService config, LogService log)
        : this(new MockTransport(new MockServer(env.ServerDirectory,
            new DataServerBagRules(config.Basic.Value.BagColumns, config.Basic.Value.BagRows,
                config.Item.Items.Values.Select(item => new DataServerItemDefinition(item.Id, item.Name, item.MaxStack))), log.ServerIO)), eventBus, log) { }

    /// <summary>隔离测试绑定替身传输并建立首个会话代次。</summary>
    internal NetService(INetTransport transport, EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ObjectDisposedException.ThrowIf(eventBus.IsDisposed, eventBus);
        ObjectDisposedException.ThrowIf(log.IsDisposed, log);
        Log = log;
        _eventBus = eventBus;
        _transport = transport;
        _transport.ResetSession(_generation);
        _transport.Received += OnReceived;
    }

    /// <summary>将请求冻结入队；监听只在后续固定帧执行。</summary>
    public void Send(IMessage request)
    {
        ThrowIfDisposed();
        _outbound.Enqueue(MessageCodec.Encode(request, _generation, true));
    }

    /// <summary>发送当前队列、推进服务端并分发响应，监听中新请求留到下一帧。</summary>
    public void Tick(double delta)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (_ticking) throw new InvalidOperationException("NetService 不允许重入 Tick");
        _ticking = true;
        try
        {
            var count = _outbound.Count;
            var generation = _generation;
            for (var index = 0; index < count && _outbound.TryDequeue(out var packet); index++)
            {
                try
                {
                    var message = MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), true);
                    _transport.Send(packet);
                    Log.Request(message);
                }
                catch (Exception error) { ReportError(error); }
                if (IsDisposed) return;
                if (_generation != generation) break;
            }
            try { _transport.Tick(delta); }
            catch (Exception error) { ReportError(error); }
            while (!IsDisposed && _inbound.TryDequeue(out var packet))
            {
                try
                {
                    var envelope = MessageCodec.ReadEnvelope(packet);
                    if (envelope.Generation == _generation)
                    {
                        var message = MessageCodec.ReadPayload(envelope, false);
                        Log.Receive(message);
                        _eventBus.Publish(message);
                    }
                }
                catch (Exception error) { ReportError(error); }
            }
        }
        finally { _ticking = false; }
    }

    /// <summary>丢弃旧会话请求、延迟消息及当前事件快照。</summary>
    public void ResetSession()
    {
        ThrowIfDisposed();
        _generation++;
        _outbound.Clear();
        _inbound.Clear();
        _eventBus.ResetPending();
        _transport.ResetSession(_generation);
    }

    /// <summary>释放传输及网络队列，事件服务由 GameServices 管理。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        try
        {
            _transport.Received -= OnReceived;
            _transport.Dispose();
        }
        finally
        {
            _outbound.Clear();
            _inbound.Clear();
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
