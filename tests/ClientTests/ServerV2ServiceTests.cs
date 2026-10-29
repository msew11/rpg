using Rpg.Config;
using Rpg.Net;
using Rpg.ProtocolV2;
using Rpg.Services;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>在真实引擎中验证 openspec/specs/server-v2-account 的配置借用、Net 收发与所有权。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2ServiceTests
{
    /// <summary>默认入口只报告未实现业务，成功链路由明确的内存测试处理器验证。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NetOwnsV2ServiceAndPublishesOnlyDuringItsTick(bool successfulHandler)
    {
        using var env = ConfigTestFixture.CreateEnv();
        using var config = new ConfigService(env);
        using var events = new EventBusService();
        using var log = new LogService(Console.WriteLine);
        var modules = successfulHandler ? new ServerModules(_ => new ActorHandlers<LoginContext>()
            .Register<DataLogin, DataLoginResult>(new ServerV2NetworkTests.DoubleLoginHandler())) : null;
        using var server = new MockServerService(config, modules);
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
}
