using System.Net;
using System.Net.Sockets;
using Rpg.Config;
using Rpg.Net;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Hosting;
using Rpg.Services;
using Codec = Rpg.ServerV2.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>验证 server-v2-hosting：真实 TCP 登录、延迟创建、复用、客户端隔离及后台保存屏障。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2TcpTests : IDisposable
{
    /// <summary>本例独立存档目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-tcp-" + Guid.NewGuid());
    /// <summary>运行时无服务器；真实连接后登录；远端服务持有者仍为空。</summary>
    [Fact]
    public async Task LoginCreatesHostOnlyOnDemandAndPureClientNeverCreatesServer()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root);
        using var config = new ConfigService(env);
        using var events = new EventBusService(); using var log = new LogService(_ => { });
        using var host = new NetService(env, events, config, log);
        Assert.True(host.IsV2); Assert.Null(host.MockServer!.Server); Assert.False(Directory.Exists(_root));
        var port = FreePort(); await host.ConnectAsync(new(true, Port: port));
        var server = host.MockServer.Server; Assert.NotNull(server);
        await host.ConnectAsync(new(true, Port: port)); Assert.Same(server, host.MockServer.Server);
        using var remoteEvents = new EventBusService();
        using var remote = new NetService(env, remoteEvents, config, log);
        await remote.ConnectAsync(new(false, "127.0.0.1", port)); Assert.Null(remote.MockServer!.Server);
        var replies = 0; using var listener = remoteEvents.Subscribe<LoginRt>(_ => replies++);
        remote.ResetSession(); remote.ResetSession(); remote.Send(new Login { Username = "demo", Password = "demo" });
        remote.Tick(0);
        await Task.Delay(100); Assert.Equal(0, replies);
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (replies == 0 && DateTime.UtcNow < limit) { remote.Tick(0); await Task.Delay(1); }
        Assert.Equal(1, replies); Assert.Null(remote.MockServer.Server);
        remote.FlushServerWrites(); Assert.Same(server, host.MockServer.Server);
    }
    /// <summary>实际 socket 接受代次跳号，同请求号在不同连接返回各自的响应。</summary>
    [Fact]
    public async Task RealSocketsSupportFragmentedPacketsAndGenerationJumps()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var holder = new MockServerService(env, config); var port = FreePort(); await holder.StartAsync(port);
        using var one = new TcpClient(); using var two = new TcpClient();
        await one.ConnectAsync(IPAddress.Loopback, port); await two.ConnectAsync(IPAddress.Loopback, port);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var packet = Codec.Encode(new Login { Username = "demo", Password = "demo" }, 5, 7, true);
        using var framed = new MemoryStream(); await TcpFraming.WriteAsync(framed, packet, default);
        foreach (var value in framed.ToArray()) await one.GetStream().WriteAsync(new byte[] { value }, timeout.Token);
        await TcpFraming.WriteAsync(two.GetStream(), Codec.Encode(new Login { Username = "demo", Password = "bad" }, 1, 7, true), timeout.Token);
        var first = Codec.ReadEnvelope((await TcpFraming.ReadAsync(one.GetStream(), timeout.Token))!);
        var second = Codec.ReadEnvelope((await TcpFraming.ReadAsync(two.GetStream(), timeout.Token))!);
        Assert.Equal(5UL, first.Generation); Assert.IsType<LoginRt>(Codec.ReadPayload(first, false));
        Assert.Equal(1UL, second.Generation); Assert.IsType<ErrorRt>(Codec.ReadPayload(second, false));
        Assert.Equal(7UL, first.RequestId); Assert.Equal(7UL, second.RequestId);
    }
    /// <summary>监听失败不交付候选服务器，原占用者不受影响。</summary>
    [Fact]
    public async Task ListenFailureLeavesHolderEmpty()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var holder = new MockServerService(env, config);
        var listener = new TcpListener(IPAddress.Any, 0); listener.Start();
        try { await Assert.ThrowsAnyAsync<SocketException>(() => holder.StartAsync(((IPEndPoint)listener.LocalEndpoint).Port)); Assert.Null(holder.Server); }
        finally { listener.Stop(); }
    }
    /// <summary>获取独立测试端口。</summary>
    internal static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    /// <summary>只清理本例创建的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
