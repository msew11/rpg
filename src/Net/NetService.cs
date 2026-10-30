using Rpg.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Google.Protobuf;
using Rpg.Services;
using Rpg.Server.Hosting;
using ProtocolCodec = Rpg.Server.Protocol.MessageCodec;

namespace Rpg.Net;

/// <summary>管理请求队列、protobuf 字节和按类型分发的服务器消息。</summary>
public sealed class NetService : Service
{
    /// <summary>构造绑定的通信日志，生命周期长于网络。</summary>
    internal LogService Log { get; }
    /// <summary>当前使用的可替换传输。</summary>
    private INetTransport _transport;
    /// <summary>当前连接是否由登录界面配置。</summary>
    public bool UsesTcp => _transport is TcpClientTransport;
    /// <summary>主动返回登录时结束玩家连接，监听服务器继续运行；被踢流程不调用。</summary>
    public void DisconnectClient()
    {
        ThrowIfDisposed();
        if (_transport is not TcpClientTransport tcp) return;
        _transport.Received -= OnReceived; _transport.Dispose();
        var transport = new TcpClientTransport(tcp.ServerService); transport.ResetSession(_generation);
        _transport = transport; _transport.Received += OnReceived;
    }
    /// <summary>借用已装配的服务器服务，创建默认 TCP 网络，不启动服务器。</summary>
    public NetService(MockServerService mockServer, EventBusService eventBus, LogService log)
        : this(CreateTcpTransport(mockServer, eventBus, log), eventBus, log) { }
    /// <summary>先验证借用依赖，再创建传输，避免失败时遗留资源。</summary>
    private static INetTransport CreateTcpTransport(MockServerService mockServer, EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(mockServer);
        ObjectDisposedException.ThrowIf(mockServer.IsDisposed, mockServer);
        ValidateDependencies(eventBus, log);
        return new TcpClientTransport(mockServer);
    }
    /// <summary>已有本机服务器是否需要配置切换确认。</summary>
    public bool RequiresServerStop(ConnectionOptions options) => _transport is TcpClientTransport tcp && tcp.ServerService.Server is { } server && (!options.HostServer || server.Port != options.Port);
    /// <summary>确认配置后连接，失败不停止相同配置的本机服务器。</summary>
    public async System.Threading.Tasks.Task ConnectAsync(ConnectionOptions options, bool stopExisting = false)
    {
        ThrowIfDisposed(); options.Validate();
        if (_transport is not TcpClientTransport tcp) throw new InvalidOperationException("当前宿主不支持 TCP 配置");
        var mockServer = tcp.ServerService;
        if (RequiresServerStop(options))
        {
            if (!stopExisting) throw new InvalidOperationException("切换配置需要确认停止原服务器");
            _transport.Received -= OnReceived; _transport.Dispose();
            await mockServer.StopAsync();
        }
        if (options.HostServer) await mockServer.StartAsync(options.Port);
        var candidate = new TcpClientTransport(mockServer); candidate.ResetSession(_generation);
        try { await candidate.ConnectAsync(options.HostServer ? "127.0.0.1" : options.Ip, options.Port); }
        catch { candidate.Dispose(); throw; }
        _transport.Received -= OnReceived; _transport.Dispose();
        _transport = candidate; _transport.Received += OnReceived;
    }
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

    /// <summary>隔离测试绑定替身传输并建立首个会话代次。</summary>
    internal NetService(INetTransport transport, EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ValidateDependencies(eventBus, log);
        Log = log;
        _eventBus = eventBus;
        _transport = transport;
        _transport.ResetSession(_generation);
        _transport.Received += OnReceived;
    }

    /// <summary>隔离回归显式选择进程内传输，不接管服务器服务。</summary>
    internal static NetService CreateInProcess(MockServerService mockServer, EventBusService eventBus, LogService log)
        => new(CreateInProcessTransport(mockServer, eventBus, log), eventBus, log);

    /// <summary>统一拒绝缺失或已释放的网络前置服务。</summary>
    private static void ValidateDependencies(EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(log);
        ObjectDisposedException.ThrowIf(eventBus.IsDisposed, eventBus);
        ObjectDisposedException.ThrowIf(log.IsDisposed, log);
    }

    /// <summary>先验证服务依赖，再建立连接，避免非法装配遗留连接。</summary>
    private static INetTransport CreateInProcessTransport(MockServerService server, EventBusService eventBus, LogService log)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(log);
        ObjectDisposedException.ThrowIf(server.IsDisposed, server);
        ObjectDisposedException.ThrowIf(eventBus.IsDisposed, eventBus);
        ObjectDisposedException.ThrowIf(log.IsDisposed, log);
        return new MockServerTransport(server);
    }

    /// <summary>将请求冻结入队；监听只在后续固定帧执行。</summary>
    public void Send(IMessage request)
    {
        ThrowIfDisposed();
        if (_transport is TcpClientTransport tcp && tcp.ServerService.Server is not null && request is Rpg.Protocol.Login login &&
            (login.Username.Trim().ToLowerInvariant() != "admin" || login.Password != "123"))
            throw new InvalidOperationException("服务器模式必须使用 admin/123 登录");
        var id = checked(++_revision);
        _outbound.Enqueue(ProtocolCodec.Encode(request, checked((ulong)_generation), checked((ulong)id), true));
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
        if (_transport is TcpClientTransport tcp)
        {
            if (tcp.ServerService.Server is null) return;
            Tick(0); tcp.WaitResponsesAsync().GetAwaiter().GetResult();
            tcp.ServerService.FlushAsync().GetAwaiter().GetResult(); return;
        }
        if (_transport is not IServerWriteBackTransport local) return;
        Tick(0); local.FlushServerWrites();
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

    /// <summary>按正式协议表解析独立业务消息。</summary>
    private IMessage ReadMessage(byte[] packet, bool request) => ProtocolCodec.ReadPayload(ProtocolCodec.ReadEnvelope(packet), request);

    /// <summary>先按当前版本检查代次，再解析并发布业务载荷。</summary>
    private IMessage? ReadCurrentMessage(byte[] packet)
    {
        var message = ProtocolCodec.ReadEnvelope(packet);
        return message.Generation == checked((ulong)_generation) ? ProtocolCodec.ReadPayload(message, false) : null;
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
