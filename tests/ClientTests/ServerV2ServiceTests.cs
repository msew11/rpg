using Rpg.Config;
using Rpg.Net;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Runtime;
using Rpg.Services;

namespace ClientTests;

/// <summary>在真实引擎中验证 openspec/specs/server-v2-account 的配置借用、Net 收发与所有权。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2ServiceTests : IDisposable
{
    /// <summary>真实服务测试使用隔离存档，不依赖工作目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-v2-service-" + Guid.NewGuid());
    /// <summary>默认入口只报告未实现业务，成功链路由明确的内存测试处理器验证。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NetOwnsV2ServiceAndPublishesOnlyDuringItsTick(bool successfulHandler)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        using var events = new EventBusService();
        using var log = new LogService(Console.WriteLine);
        var modules = successfulHandler ? new ServerModules(new DoubleServerV2Repo(), new DoubleServerV2Repo(), _ => new ActorHandlers<LoginContext>()
            .Register<Login, LoginRt>(new ServerV2NetworkTests.DoubleLoginHandler()), config: config) : null;
        using var server = new MockServerService(env, config, modules);
        using var net = new NetService(server, events, log);
        Assert.Same(config, server.Config);
        Assert.Same(server, net.MockServer);
        var messages = 0;
        var thread = Environment.CurrentManagedThreadId;
        using var success = events.Subscribe<LoginRt>(_ => { Assert.Equal(thread, Environment.CurrentManagedThreadId); messages++; });
        using var failure = events.Subscribe<ErrorRt>(message =>
        { Assert.Equal(thread, Environment.CurrentManagedThreadId); Assert.Equal(ErrorCodePb.NotImplemented, message.Code); messages++; });
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
        Assert.True(server.IsDisposed);
        Assert.False(config.IsDisposed);
        Assert.NotEmpty(config.Map.Items);
        Assert.Throws<ObjectDisposedException>(() => server.OpenConnection());
        Assert.Throws<ObjectDisposedException>(() => net.Send(new Login()));
        net.Dispose();
        server.Dispose();
    }
    /// <summary>只清理本例分配的临时存档目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
