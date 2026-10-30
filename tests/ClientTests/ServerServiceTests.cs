using Rpg.Config;
using Rpg.Net;
using Rpg.Protocol;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules;
using Rpg.Server.Runtime;
using Rpg.Services;

namespace ClientTests;

/// <summary>在真实引擎中验证 openspec/specs/server-account 的配置借用、Net 收发与借用边界。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerServiceTests : IDisposable
{
    /// <summary>真实服务测试使用隔离存档，不依赖工作目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-service-" + Guid.NewGuid());
    /// <summary>默认入口拒绝错误凭据，注入处理器验证固定帧分发及借用生命周期。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetBorrowsServiceAndPublishesOnlyDuringItsTick(bool successfulHandler)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        using var events = new EventBusService();
        using var log = new LogService(Console.WriteLine);
        using var server = successfulHandler
            ? new MockServerService(env, new ServerModules(config, new DoubleServerRepo(), new DoubleServerRepo(), new DoubleServerRepo(), _ => new ActorHandlers<LoginContext>()
                .Register<Login, LoginRt>(new ServerNetworkTests.DoubleLoginHandler())))
            : MockServerService.CreateInProcess(env, config);
        using var net = NetService.CreateInProcess(server, events, log);
        Assert.Same(config, server.Config);
        var messages = 0;
        var thread = Environment.CurrentManagedThreadId;
        using var success = events.Subscribe<LoginRt>(_ => { Assert.Equal(thread, Environment.CurrentManagedThreadId); messages++; });
        using var failure = events.Subscribe<ErrorRt>(message =>
        { Assert.Equal(thread, Environment.CurrentManagedThreadId); Assert.Equal(ErrorCodePb.InvalidCredentials, message.Code); messages++; });
        var errors = new List<Exception>();
        net.Error += errors.Add;
        net.Send(new Login { Username = " Alice ", Password = "not-validated" });
        Assert.Equal(0, messages);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (messages == 0 && DateTime.UtcNow < deadline)
        { net.Tick(0); Thread.Sleep(1); }
        Assert.Equal(1, messages);
        Assert.Empty(errors);
        net.ResetSession();
        net.Tick(0);
        net.Dispose();
        Assert.False(server.IsDisposed);
        Assert.False(config.IsDisposed);
        Assert.NotEmpty(config.Map.Items);
        var next = server.OpenConnection();
        await server.CloseConnectionAsync(next);
        Assert.Throws<ObjectDisposedException>(() => net.Send(new Login()));
        net.Dispose();
        server.Dispose();
    }
    /// <summary>只清理本例分配的临时存档目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
