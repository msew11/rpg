using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Rpg.Net;

namespace Rpg.Server.Hosting;

/// <summary>真实 TCP 监听与连接所有者，不处理角色业务。</summary>
internal sealed class TcpServerHost : IAsyncDisposable
{
    /// <summary>借用的服务器运行时。</summary>
    private readonly ServerRuntime _runtime;
    /// <summary>监听套接字。</summary>
    private readonly TcpListener _listener;
    /// <summary>停止监听及所有连接的信号。</summary>
    private readonly CancellationTokenSource _stop = new();
    /// <summary>活动客户端及其排空任务。</summary>
    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    /// <summary>接纳循环。</summary>
    private readonly Task _accept;
    /// <summary>独立诊断入口。</summary>
    private readonly Action<Exception> _report;
    /// <summary>实际监听端口。</summary>
    internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    /// <summary>绑定双栈端口；平台无 IPv6 时退回 IPv4。</summary>
    internal TcpServerHost(ServerRuntime runtime, int port, Action<Exception> report)
    {
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _runtime = runtime; _report = report;
        _listener = new TcpListener(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, port);
        _listener.ExclusiveAddressUse = true;
        if (Socket.OSSupportsIPv6) _listener.Server.DualMode = true;
        try { _listener.Start(); }
        catch { _listener.Stop(); _stop.Dispose(); throw; }
        _accept = Task.Run(AcceptAsync);
    }
    /// <summary>每条连接拥有独立收发与串行请求。</summary>
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                client.NoDelay = true; client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _clients[client] = completion.Task;
                _ = RunClientAsync(client, completion);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }
    /// <summary>接收先完成请求再读下一包，输出由独立循环推进。</summary>
    private async Task RunClientAsync(TcpClient client, TaskCompletionSource completion)
    {
        var id = _runtime.OpenConnection();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var stream = client.GetStream();
        var output = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    while (_runtime.TryReadOutgoing(id, out var packet))
                        await TcpFraming.WriteAsync(stream, packet, cancellation.Token).ConfigureAwait(false);
                    await _runtime.WaitOutgoingAsync(id, cancellation.Token).ConfigureAwait(false);
                }
            }
            finally { cancellation.Cancel(); client.Close(); }
        });
        try
        {
            while (await TcpFraming.ReadAsync(stream, cancellation.Token).ConfigureAwait(false) is { } packet)
                await _runtime.ReceiveAsync(id, packet).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        catch (Exception error) { _report(error); }
        finally
        {
            cancellation.Cancel(); client.Close();
            try { await output.ConfigureAwait(false); } catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException) { }
            try { await _runtime.CloseConnectionAsync(id).ConfigureAwait(false); } catch (Exception error) { _report(error); }
            _clients.TryRemove(client, out _); completion.TrySetResult(); client.Dispose();
        }
    }
    /// <summary>停止接纳并等待每个连接清理完成。</summary>
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _listener.Stop(); await _accept.ConfigureAwait(false);
        foreach (var client in _clients.Keys) client.Close();
        await Task.WhenAll(_clients.Values.ToArray()).ConfigureAwait(false); _stop.Dispose();
    }
}
