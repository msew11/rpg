using Rpg.Config;
using Rpg.Config.Data;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules.World;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-account 的上下文配置入口、实例隔离和借用生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2ConfigTests
{
    /// <summary>三个模块的处理器读取各自宿主配置，停止服务不释放配置，释放配置后上下文拒绝读取。</summary>
    [Fact]
    public async Task HandlersReadOwningServiceConfigThroughAllModuleContexts()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: Path.Combine(Path.GetTempPath(), "rpg-v2-config-" + Guid.NewGuid()));
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

        ModuleContext[] contexts = [new LoginContext(firstConfig, "alice", new([])),
            new RoleContext(firstConfig, 10, new(new() { RoleId = 10 }, [], [])), new WorldContext(firstConfig, new(1, 1000, 1000, 32, 32), new([]))];
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
    public void HostAndModuleEntriesRejectInvalidDependenciesBeforeWork()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-v2-entry-" + Guid.NewGuid());
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: directory);
        using var config = new ConfigService(env);
        var repo = new DoubleServerV2Repo();
        Assert.Equal("env", Assert.Throws<ArgumentNullException>(() => new MockServerService(null!, config)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new MockServerService(env, (ConfigService)null!)).ParamName);
        Assert.Equal("modules", Assert.Throws<ArgumentNullException>(() => new MockServerService(env, (ServerModules)null!)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new ServerModules(null!, repo, repo, new DoubleServerV2Repo(), _ => new())).ParamName);
        Assert.Equal("summaries", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, null!, repo, new DoubleServerV2Repo(), _ => new())).ParamName);
        Assert.Equal("roles", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, repo, null!, new DoubleServerV2Repo(), _ => new())).ParamName);
        Assert.Equal("worlds", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, repo, repo, null!, _ => new())).ParamName);
        Assert.Equal("login", Assert.Throws<ArgumentNullException>(() => new ServerModules(config, repo, repo, new DoubleServerV2Repo(), null!)).ParamName);
        Assert.Equal("config", Assert.Throws<ArgumentNullException>(() => new WorldContext(null!, new(1, 1000, 1000, 32, 32), new([]))).ParamName);
        using var disposedConfig = new ConfigService(env);
        disposedConfig.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new MockServerService(env, disposedConfig));
        Assert.Throws<ObjectDisposedException>(() => Modules(disposedConfig));
        Assert.Throws<ObjectDisposedException>(() => new WorldContext(disposedConfig, new(1, 1000, 1000, 32, 32), new([])));
        using var disposedEnv = ConfigTestFixture.CreateEnv(serverDirectory: directory);
        disposedEnv.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new MockServerService(disposedEnv, config));
        Assert.False(Directory.Exists(directory));
        Assert.False(config.IsDisposed);
    }

    /// <summary>为三个真实 Actor 注册同一无字段处理器契约，配置仅由 Context 提供。</summary>
    private static ServerModules Modules(ConfigService config) => new(config, new DoubleServerV2Repo(), new DoubleServerV2Repo(), new DoubleServerV2Repo(),
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
