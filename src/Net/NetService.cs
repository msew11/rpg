using Rpg.Server.Map;
using Rpg.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Google.Protobuf;
using Rpg.Services;
using Rpg.Server;
using Rpg.Server.Handler.Bag;
using Rpg.ServerV2.Hosting;
using V2Codec = Rpg.ServerV2.Protocol.MessageCodec;

namespace Rpg.Net;

/// <summary>管理请求队列、protobuf 字节和按类型分发的服务器消息。</summary>
public sealed class NetService : Service
{
    /// <summary>构造绑定的通信日志，生命周期长于网络。</summary>
    internal LogService Log { get; }
    /// <summary>当前使用的可替换传输。</summary>
    private readonly INetTransport _transport;
    /// <summary>显式 V2 模式下由本 Net 拥有的服务端子服务；旧链路为空。</summary>
    private readonly MockServerService? _mockServer;
    /// <summary>当前 Net 拥有的 V2 服务入口，不另建全局服务注册。</summary>
    public MockServerService? MockServer => _mockServer;
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
    /// <summary>本发送端不落地的递增消息序号。</summary>
    private long _revision;

    /// <summary>用于会话所有权检查的当前代次。</summary>
    internal long Generation => _generation;
    /// <summary>报告编解码与传输错误。</summary>
    public event Action<Exception>? Error;

    /// <summary>在服务内部初始化当前阶段的进程内传输。</summary>
    public NetService(EnvService env, EventBusService eventBus, ConfigService config, LogService log)
        : this(new MockTransport(new MockServer(env.ServerDirectory,
            new BagRules(config.Basic.Value.BagColumns, config.Basic.Value.BagRows,
                config.Item.Items.Values.ToArray()),
            log.ServerIO, () => env.ServerFlushIntervalSeconds,
            config.Map.Items.Values.Select(map => new MapLayout(map.Id, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles)),
            config.Basic.Value.InitialMapId, () => env.MovementSpeed)), eventBus, log) { }

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

    /// <summary>显式接入 V2 并接管服务生命周期；迁移完成前不替换默认旧入口。</summary>
    public NetService(MockServerService mockServer, EventBusService eventBus, LogService log)
        : this(CreateV2Transport(mockServer, eventBus, log), eventBus, log) => _mockServer = mockServer;

    /// <summary>先验证服务依赖，再建立连接，避免非法装配遗留连接。</summary>
    private static INetTransport CreateV2Transport(MockServerService server, EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(log);
        ObjectDisposedException.ThrowIf(server.IsDisposed, server);
        ObjectDisposedException.ThrowIf(eventBus.IsDisposed, eventBus);
        ObjectDisposedException.ThrowIf(log.IsDisposed, log);
        return new MockServerV2Transport(server);
    }

    /// <summary>将请求冻结入队；监听只在后续固定帧执行。</summary>
    public void Send(IMessage request)
    {
        ThrowIfDisposed();
        var id = checked(++_revision);
        _outbound.Enqueue(_mockServer is null ? MessageCodec.Encode(request, _generation, true, id)
            : V2Codec.Encode(request, checked((ulong)_generation), checked((ulong)id), true));
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
                    if (Log.Enabled)
                        Log.Request(ReadMessage(packet, true));
                    _transport.Send(packet);
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
                    if (ReadCurrentMessage(packet) is { } message)
                    {
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

    /// <summary>离开角色前发送已排队请求，再提交本地服务端已接纳的状态。</summary>
    public void FlushServerWrites()
    {
        ThrowIfDisposed();
        if (_transport is not IServerWriteBackTransport local) return;
        Tick(0);
        local.FlushServerWrites();
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
            try { _mockServer?.Dispose(); }
            finally
            {
                _outbound.Clear();
                _inbound.Clear();
                Error = null;
            }
        }
    }

    /// <summary>接收方拥有独立字节，避免对端修改已入队数据。</summary>
    private void OnReceived(byte[] packet) => _inbound.Enqueue((byte[])packet.Clone());

    /// <summary>按当前传输使用对应协议表，防止 V1/V2 类型混用。</summary>
    private IMessage ReadMessage(byte[] packet, bool request) => _mockServer is null
        ? MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), request)
        : V2Codec.ReadPayload(V2Codec.ReadEnvelope(packet), request);

    /// <summary>先按当前版本检查代次，再解析并发布业务载荷。</summary>
    private IMessage? ReadCurrentMessage(byte[] packet)
    {
        if (_mockServer is null)
        {
            var envelope = MessageCodec.ReadEnvelope(packet);
            return envelope.Generation == _generation ? MessageCodec.ReadPayload(envelope, false) : null;
        }
        var v2 = V2Codec.ReadEnvelope(packet);
        return v2.Generation == checked((ulong)_generation) ? V2Codec.ReadPayload(v2, false) : null;
    }

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
