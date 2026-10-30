using Rpg.Config;
using Rpg.Config.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.World;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-account 的上下文配置入口、实例隔离和借用生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerConfigTests
{
    /// <summary>三个模块的处理器读取各自宿主配置，停止服务不释放配置，释放配置后上下文拒绝读取。</summary>
    [Fact]
    public async Task HandlersReadOwningServiceConfigThroughAllModuleContexts()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: Path.Combine(Path.GetTempPath(), "rpg-server-config-" + Guid.NewGuid()));
        using var firstConfig = new ConfigService(env);
        using var secondConfig = new ConfigService(env);
        var firstModules = Modules(firstConfig);
        var secondModules = Modules(secondConfig);
        await using var first = new MockServerService(env, firstModules);
        await using var second = new MockServerService(env, secondModules);
        Assert.NotSame(firstConfig.Basic.Value, secondConfig.Basic.Value);
        foreach (var actor in new[] { firstModules.Login("alice"), firstModules.Role(10), firstModules.World(1) })
            Assert.Same(firstConfig.Basic.Value, await actor.Ask(new DataReadConfig()));
        foreach (var actor in new[] { secondModules.Login("alice"), secondModules.Role(10), secondModules.World(1) })
            Assert.Same(secondConfig.Basic.Value, await actor.Ask(new DataReadConfig()));

        ModuleContext[] contexts = [new LoginContext(firstConfig, firstModules, "alice", new([])),
            new RoleContext(firstConfig, firstModules, 10, new(new() { RoleId = 10 }, [], [])), new WorldContext(firstConfig, firstModules, new(1, 1000, 1000, 32, 32), new([]))];
        Assert.All(contexts, context => Assert.Same(firstConfig, context.Config));
        await first.DisposeAsync();
        Assert.False(firstConfig.IsDisposed);
        Assert.NotEmpty(firstConfig.Map.Items);
        firstConfig.Dispose();
        Assert.All(contexts, context => Assert.Throws<ObjectDisposedException>(() => context.Config));
        Assert.Same(secondConfig.Basic.Value, await secondModules.Login("alice").Ask(new DataReadConfig()));
    }

    /// <summary>验证入口拒绝契约：缺失或失效依赖同步失败，早于分片创建及存档 IO。</summary>
    [Fact]
    public async Task HostAndModuleEntriesRejectInvalidDependenciesBeforeWork()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-server-entry-" + Guid.NewGuid());
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: directory);
        using var config = new ConfigService(env);
        var repo = new DoubleServerRepo();
        await using var modules = Modules(config);
        Assert.Equal("env", Assert.Throws<ArgumentNullException>(() => new MockServerService(null!, config)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new MockServerService(env, (ConfigService)null!)).ParamName);
        Assert.Equal("modules", Assert.Throws<ArgumentNullException>(() => new MockServerService(env, (IServerModules)null!)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new ServerModules(null!, repo, repo, new DoubleServerRepo())).ParamName);
        Assert.Equal("summaries", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, null!, repo, new DoubleServerRepo())).ParamName);
        Assert.Equal("roles", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, repo, null!, new DoubleServerRepo())).ParamName);
        Assert.Equal("worlds", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, repo, repo, null!)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new WorldContext(null!, modules, new(1, 1000, 1000, 32, 32), new([]))).ParamName);
        using var disposedConfig = new ConfigService(env);
        disposedConfig.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new MockServerService(env, disposedConfig));
        Assert.Throws<ObjectDisposedException>(() => Modules(disposedConfig));
        Assert.Throws<ObjectDisposedException>(() => new WorldContext(disposedConfig, modules, new(1, 1000, 1000, 32, 32), new([])));
        using var disposedEnv = ConfigTestFixture.CreateEnv(serverDirectory: directory);
        disposedEnv.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new MockServerService(disposedEnv, config));
        Assert.False(Directory.Exists(directory));
        Assert.False(config.IsDisposed);
    }

    /// <summary>为三个真实 Actor 注册同一无字段处理器契约，配置仅由 Context 提供。</summary>
    private static DoubleServerModules Modules(ConfigService config) => new(config, new DoubleServerRepo(), new DoubleServerRepo(), new DoubleServerRepo(),
        _ => new ActorHandlers<LoginContext>().Register<DataReadConfig, BasicCfg>(new DoubleConfigHandler<LoginContext>()),
        _ => new ActorHandlers<RoleContext>().Register<DataReadConfig, BasicCfg>(new DoubleConfigHandler<RoleContext>()),
        _ => new ActorHandlers<WorldContext>().Register<DataReadConfig, BasicCfg>(new DoubleConfigHandler<WorldContext>()));

    /// <summary>测试读取已加载配置，不在消息中携带服务或操作委托。</summary>
    private sealed record DataReadConfig : IActorRequest<BasicCfg>;

    /// <summary>服务于配置读取契约的无状态替身，不持有任何配置依赖。</summary>
    private sealed class DoubleConfigHandler<TContext> : IActorHandler<TContext, DataReadConfig, BasicCfg> where TContext : ModuleContext
    {
        /// <summary>通过统一入口返回缓存中的只读配置数据，用实例一致性验证注入链路。</summary>
        public ValueTask<BasicCfg> HandleAsync(TContext context, DataReadConfig request) => ValueTask.FromResult(context.Config.Basic.Value);
    }
}
