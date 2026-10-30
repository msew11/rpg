using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Rpg.ServerV2.Hosting;
using Codec = Rpg.ServerV2.Protocol.MessageCodec;

namespace Rpg.Net;

/// <summary>真实客户端传输；后台记录响应屏障，固定帧交付消息。</summary>
internal sealed class TcpClientTransport : INetTransport
{
    /// <summary>借用统一装配的非空服务器服务，传输关闭不释放服务。</summary>
    internal MockServerService ServerService { get; }
    /// <summary>绑定同次启动的服务器持有服务，服务器实例可以尚未启动。</summary>
    internal TcpClientTransport(MockServerService server)
    {
        ArgumentNullException.ThrowIfNull(server);
        ObjectDisposedException.ThrowIf(server.IsDisposed, server);
        ServerService = server;
    }
    /// <summary>本次套接字生命周期。</summary>
    private readonly CancellationTokenSource _stop = new();
    /// <summary>套接字连接。</summary>
    private TcpClient? _client;
    /// <summary>唯一后台写入者的消息通道。</summary>
    private readonly Channel<byte[]> _send = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    /// <summary>后台收到但尚未交付的消息。</summary>
    private readonly ConcurrentQueue<byte[]> _received = new();
    /// <summary>请求编号与响应屏障。</summary>
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource> _pending = new();
    /// <summary>后台收发任务。</summary>
    private Task _io = Task.CompletedTask;
    /// <summary>后台失败，固定帧只报告一次。</summary>
    private Exception? _error;
    /// <summary>当前代次仅用于清理本地待派发消息。</summary>
    private ulong _generation = 1;
    /// <summary>是否释放。</summary>
    private bool _disposed;
    /// <summary>仅 Tick 内触发的接收事件。</summary>
    public event Action<byte[]>? Received;
    /// <summary>五秒内连接指定字面地址。</summary>
    internal async Task ConnectAsync(string ip, int port)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var address = IPAddress.Parse(ip);
        _client = new TcpClient(address.AddressFamily) { NoDelay = true };
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _client.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
        _io = Task.Run(RunAsync);
    }
    /// <summary>冻结字节并登记最多十五秒的响应等待。</summary>
    public void Send(byte[] packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_client is null || !_client.Connected) throw new IOException("尚未连接服务器");
        if (_error is { } error) throw new IOException("连接已经失效", error);
        var envelope = Codec.ReadEnvelope(packet);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[envelope.RequestId] = completion;
        if (!_send.Writer.TryWrite((byte[])packet.Clone())) { _pending.TryRemove(envelope.RequestId, out _); throw new IOException("客户端发送队列已满"); }
        _ = WatchTimeoutAsync(envelope.RequestId, completion);
    }
    /// <summary>超时关闭失去响应的连接，禁止自动重放。</summary>
    private async Task WatchTimeoutAsync(ulong id, TaskCompletionSource completion)
    {
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), _stop.Token).ConfigureAwait(false); }
        catch (TimeoutException error) { Fail(error); }
        catch (OperationCanceledException) { }
        catch (Exception) { /* 原错误由固定帧或保存屏障观察。 */ }
    }
    /// <summary>收发共用取消边界，后台接收即可完成保存屏障。</summary>
    private async Task RunAsync()
    {
        var stream = _client!.GetStream();
        var write = Task.Run(async () =>
        {
            try { await foreach (var packet in _send.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false)) await TcpFraming.WriteAsync(stream, packet, _stop.Token).ConfigureAwait(false); }
            catch (Exception error) { if (!_stop.IsCancellationRequested) Fail(error); }
        });
        try
        {
            while (await TcpFraming.ReadAsync(stream, _stop.Token).ConfigureAwait(false) is { } packet)
            {
                var envelope = Codec.ReadEnvelope(packet);
                if (envelope.RequestId != 0 && _pending.TryRemove(envelope.RequestId, out var completion)) completion.TrySetResult();
                if (envelope.Generation == _generation) _received.Enqueue(packet);
            }
            if (!_stop.IsCancellationRequested) Fail(new IOException("服务器连接已关闭"));
        }
        catch (Exception error) { if (!_stop.IsCancellationRequested) Fail(error); }
        finally { _stop.Cancel(); await write.ConfigureAwait(false); }
    }
    /// <summary>固定帧派发消息和一次性错误。</summary>
    public void Tick(double delta)
    {
        while (_received.TryDequeue(out var packet)) Received?.Invoke(packet);
        if (Interlocked.Exchange(ref _error, null) is { } error) throw error;
    }
    /// <summary>本地重置不发送额外协议，下一请求携带新信封代次。</summary>
    public void ResetSession(long generation)
    {
        _generation = checked((ulong)generation); _received.Clear();
        foreach (var pair in _pending.ToArray()) if (_pending.TryRemove(pair.Key, out var completion)) completion.TrySetCanceled();
    }
    /// <summary>等待调用前已经发送的请求响应，不依赖主线程派发。</summary>
    internal Task WaitResponsesAsync() => Task.WhenAll(_pending.Values.Select(item => item.Task)).WaitAsync(TimeSpan.FromSeconds(15));
    /// <summary>使连接失败并唤醒所有保存屏障。</summary>
    private void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref _error, error, null);
        foreach (var pair in _pending.ToArray()) if (_pending.TryRemove(pair.Key, out var completion)) completion.TrySetException(error);
        _stop.Cancel(); _client?.Close();
    }
    /// <summary>先取消网络，再等待后台退出。</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _stop.Cancel(); _send.Writer.TryComplete(); _client?.Close();
        foreach (var completion in _pending.Values) completion.TrySetCanceled(); _pending.Clear();
        _io.GetAwaiter().GetResult(); _client?.Dispose(); _stop.Dispose(); Received = null;
    }
}
