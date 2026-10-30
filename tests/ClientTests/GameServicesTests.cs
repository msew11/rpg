using Rpg.Tests;
using Rpg.Config;
using Rpg.Settings;
using Rpg.Net;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Session;
using MessageCodec = Rpg.Server.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>验证 openspec/specs/game-services 的全局访问、所有权及独立实例契约；同类测试串行执行。</summary>
[Trait("Runtime", "Godot")]
public sealed class GameServicesTests
{
    /// <summary>验证 game-services 的服务器统一注册：启动为空、Net提前释放不连带释放、整体句柄统一结束。</summary>
    [Fact]
    public void ServerServiceBelongsToCompleteRegistrationAndOutlivesNet()
    {
        using var lifetime = GameServices.Initialize();
        var server = GameServices.MockServer;
        Assert.Null(server.Server);
        Assert.False(server.IsDisposed);
        GameServices.Net.Dispose();
        Assert.Same(server, GameServices.MockServer);
        Assert.False(server.IsDisposed);
        lifetime.Dispose();
        Assert.True(server.IsDisposed);
        Assert.Throws<InvalidOperationException>(() => GameServices.MockServer);
    }

    /// <summary>验证 game-services 所有公开服务构造的非空依赖契约，避免空值延迟到业务阶段报错。</summary>
    [Fact]
    public void EveryPublicServiceConstructorRejectsNullDependencies()
    {
        using var lifetime = Register(new DoubleNetTransport());
        var dependencies = typeof(GameServices).GetProperties()
            .Where(property => typeof(IService).IsAssignableFrom(property.PropertyType))
            .ToDictionary(property => property.PropertyType, property => property.GetValue(null));
        foreach (var type in dependencies.Keys)
            foreach (var constructor in type.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                for (var index = 0; index < parameters.Length; index++)
                {
                    var arguments = parameters.Select(parameter => dependencies[parameter.ParameterType]).ToArray();
                    arguments[index] = null;
                    var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => constructor.Invoke(arguments));
                    Assert.Equal(parameters[index].Name, Assert.IsType<ArgumentNullException>(error.InnerException).ParamName);
                }
            }
    }

    /// <summary>验证 game-services 的正式启动无参，公开服务构造只声明服务依赖。</summary>
    [Fact]
    public void PublicInitializationAndConstructorsExposeOnlyServiceDependencies()
    {
        var initialize = Assert.Single(typeof(GameServices).GetMethods()
            .Where(method => method.Name == nameof(GameServices.Initialize)));
        Assert.Empty(initialize.GetParameters());
        var services = typeof(Service).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IService).IsAssignableFrom(type));
        foreach (var type in services)
            foreach (var constructor in type.GetConstructors())
                Assert.All(constructor.GetParameters(), parameter =>
                    Assert.True(typeof(IService).IsAssignableFrom(parameter.ParameterType), type.Name));
    }

    /// <summary>验证 game-services 的隔离环境同时控制配置、设置和模拟服务端，且随整组释放。</summary>
    [Fact]
    public void EnvironmentControlsDirectoriesAndEndsWithItsConsumers()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-environment-" + Guid.NewGuid());
        using var env = ConfigTestFixture.CreateEnv(Path.Combine(directory, "settings"),
            Path.Combine(directory, "server"));
        try
        {
            Assert.Throws<InvalidOperationException>(() => GameServices.Env);
            using var lifetime = GameServicesTestFactory.Register(env);
            Assert.Same(env, GameServices.Env);
            Assert.Equal(1000, GameServices.Config.Map.Items[1].Width);
            GameServices.Setting.SetResolution(1600, 900);
            Assert.True(File.Exists(Path.Combine(env.SettingsDirectory, "system.json")));
            GameServices.Session.Login("demo1", "123");
            PumpUntil(() => GameServices.Session.IsLoggedIn);
            Assert.True(GameServices.Session.IsLoggedIn);
            Assert.Empty(GameServices.Session.Roles);
            GameServices.Session.CreateRole("目录验收", Rpg.Const.RoleGender.Male, 1);
            PumpUntil(() => !GameServices.Session.AccountRequestPending);
            var created = Assert.Single(GameServices.Session.Roles);
            GameServices.Session.SelectRole(created.RoleId);
            PumpUntil(() => GameServices.Session.HasRole);
            GameServices.Net.FlushServerWrites();
            Assert.True(File.Exists(Path.Combine(env.ServerDirectory, $"{created.RoleId}.role.json")));
            lifetime.Dispose();
            Assert.True(env.IsDisposed);
            Assert.Throws<InvalidOperationException>(() => GameServices.Env);
            using var nextEnv = ConfigTestFixture.CreateEnv(env.SettingsDirectory, env.ServerDirectory);
            using var next = GameServicesTestFactory.Register(nextEnv);
            lifetime.Dispose();
            Assert.Same(nextEnv, GameServices.Env);
            Assert.Equal(1600, GameServices.Setting.Width);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>等待正式异步字节链路完成。</summary>
    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { GameServices.Net.Tick(0); Thread.Sleep(1); }
        Assert.True(done());
    }

    /// <summary>验证 game-services 的早期配置失败与后续网络构造失败都会清理环境，允许重试。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstructionFailureReleasesEnvironmentAndAllowsRestart(bool failConfig)
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-environment-failure-" + Guid.NewGuid());
        var root = ConfigTestFixture.FindRoot();
        using var env = new EnvService(directory, "",
            failConfig ? Path.Combine(directory, "missing") : Path.Combine(root, "config"),
            Path.Combine(root, "schemas"));
        Assert.ThrowsAny<Exception>(() => GameServicesTestFactory.Register(env));
        Assert.True(env.IsDisposed);
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.EventBus);
        using var nextEnv = ConfigTestFixture.CreateEnv(directory, Path.Combine(directory, "server"));
        using var next = GameServicesTestFactory.Register(nextEnv);
        Assert.Same(nextEnv, GameServices.Env);
    }

    /// <summary>验证 game-services 的整体清理：网络关闭失败后仍保存布局并使其他服务失效。</summary>
    [Fact]
    public void NetworkDisposalFailureStillSavesSettingsAndRevokesAllServices()
    {
        using var eventBus = new EventBusService();
        var directory = Path.Combine(Path.GetTempPath(), "rpg-service-dispose-" + Guid.NewGuid());
        try
        {
            var config = ConfigTestFixture.LoadDefault();
            var env = ConfigTestFixture.CreateEnv(directory);
            var setting = new SettingService(env, new Rpg.Services.LogService(Console.WriteLine));
            var net = new NetService(new DoubleLifetimeTransport { FailDispose = true }, eventBus, new Rpg.Services.LogService(Console.WriteLine));
            using var services = GameServicesTestFactory.Register(env, eventBus, config, setting, net);
            var session = GameServices.Session;
            var world = GameServices.World;
            var animation = GameServices.Animation;
            setting.SelectRole(1001);
            setting.SetHudPosition("portrait", 0.3f, 0.4f);
            Assert.Throws<IOException>(() => services.Dispose());
            Assert.True(((IService)net).IsDisposed);
            Assert.True(((IService)setting).IsDisposed);
            Assert.Throws<InvalidOperationException>(() => GameServices.Config);
            Assert.Throws<InvalidOperationException>(() => GameServices.Setting);
            Assert.Throws<InvalidOperationException>(() => GameServices.Session);
            Assert.Throws<InvalidOperationException>(() => GameServices.World);
            Assert.Throws<InvalidOperationException>(() => GameServices.Animation);
            Assert.Throws<ObjectDisposedException>(() => config.Map.Items[1]);
            Assert.Throws<ObjectDisposedException>(() => setting.SetResolution(1280, 720));
            Assert.Throws<ObjectDisposedException>(() => session.SelectRole(1001));
            Assert.Throws<ObjectDisposedException>(() => world.Tick(0));
            Assert.Throws<ObjectDisposedException>(() => animation.Get(Rpg.Const.RoleGender.Male, Rpg.Const.RoleMotion.Stand));
            Assert.Equal(0.3f, SettingsTestFixture.ReadRole(directory, 1001).HudPositions["portrait"].X);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>验证 game-services 的整体清理：设置保存失败也不阻止网络和配置清理或下次注册。</summary>
    [Fact]
    public void SettingsDisposalFailureStillRevokesDependenciesAndAllowsRestart()
    {
        using var eventBus = new EventBusService();
        var directory = Path.Combine(Path.GetTempPath(), "rpg-service-setting-failure-" + Guid.NewGuid());
        try
        {
            var config = ConfigTestFixture.LoadDefault();
            var env = ConfigTestFixture.CreateEnv(directory);
            var setting = new SettingService(env, new Rpg.Services.LogService(Console.WriteLine));
            var net = new NetService(new DoubleNetTransport(), eventBus, new Rpg.Services.LogService(Console.WriteLine));
            using var services = GameServicesTestFactory.Register(env, eventBus, config, setting, net);
            setting.SelectRole(1001);
            setting.SetHudPosition("portrait", 0.3f, 0.4f);
            File.WriteAllText(directory, "阻止创建设置目录");
            Assert.ThrowsAny<IOException>(() => services.Dispose());
            Assert.True(((IService)setting).IsDisposed);
            Assert.True(((IService)net).IsDisposed);
            Assert.False(GameServices.IsInitialized);
            Assert.Throws<ObjectDisposedException>(() => net.Send(new Login()));
            Assert.Throws<ObjectDisposedException>(() => config.Map.Items[1]);
            using var restarted = Register(new DoubleNetTransport());
            Assert.False(GameServices.Session.IsLoggedIn);
            Assert.False(GameServices.World.HasWorld);
            services.Dispose();
            Assert.True(GameServices.IsInitialized);
        }
        finally { if (File.Exists(directory)) File.Delete(directory); }
    }

    /// <summary>注册前明确报错，注册后发送和监听仍遵循 client-network 的固定帧队列。</summary>
    [Fact]
    public void GlobalAccessRequiresRegistrationAndKeepsQueuedDelivery()
    {
        using var eventBus = new EventBusService();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        using var transport = new DoubleNetTransport();
        var net = new NetService(transport, eventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var services = Register(net, eventBus);
        Assert.True(GameServices.IsInitialized);
        Assert.Same(net, GameServices.Net);
        var delivered = 0;
        using var subscription = GameServices.EventBus.Subscribe<LoginRt>(_ => delivered++);
        GameServices.Net.Send(new Login { Username = "demo" });
        Assert.Empty(transport.Sent);
        GameServices.Net.Tick(0);
        var generation = MessageCodec.ReadEnvelope(Assert.Single(transport.Sent)).Generation;
        transport.Inject(MessageCodec.Encode(new LoginRt(), generation, 1, false));
        Assert.Equal(0, delivered);
        GameServices.Net.Tick(0);
        Assert.Equal(1, delivered);
    }

    /// <summary>空对象和已释放实例不可注册，重复注册不替换或接管另一有效实例。</summary>
    [Fact]
    public void InvalidAndDuplicateRegistrationLeaveOwnershipUnchanged()
    {
        using var eventBus = new EventBusService();
        Assert.Throws<ArgumentNullException>(() => Register(null!, eventBus));
        using var disposed = new NetService(new DoubleNetTransport(), eventBus, new Rpg.Services.LogService(Console.WriteLine));
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => Register(disposed, eventBus));
        Assert.False(GameServices.IsInitialized);
        var original = new NetService(new DoubleNetTransport(), eventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var services = Register(original, eventBus);
        using var otherTransport = new DoubleNetTransport();
        using var other = new NetService(otherTransport, eventBus, new Rpg.Services.LogService(Console.WriteLine));
        Assert.Throws<InvalidOperationException>(() => Register(other, eventBus));
        Assert.Same(original, GameServices.Net);
        other.Send(new Login());
        other.Tick(0);
        Assert.Single(otherTransport.Sent);
    }

    /// <summary>句柄释放服务及监听，重建后旧句柄的再次释放不影响新服务。</summary>
    [Fact]
    public void LifetimeDisposesNetworkAndCannotReleaseLaterRegistration()
    {
        using var eventBus = new EventBusService();
        var transport = new DoubleLifetimeTransport();
        var net = new NetService(transport, eventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var services = Register(net, eventBus);
        var oldEvents = GameServices.EventBus;
        var oldCalls = 0;
        using var subscription = oldEvents.Subscribe<int>(_ => oldCalls++);
        services.Dispose();
        services.Dispose();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        Assert.Throws<ObjectDisposedException>(() => net.Send(new Login()));
        Assert.Throws<InvalidOperationException>(() => GameServices.EventBus);
        Assert.Throws<ObjectDisposedException>(() => oldEvents.Publish(1));
        Assert.Equal(1, transport.DisposeCount);
        using var currentEvents = new EventBusService();
        var current = new NetService(new DoubleNetTransport(), currentEvents, new Rpg.Services.LogService(Console.WriteLine));
        using var currentServices = Register(current, currentEvents);
        var newEvents = GameServices.EventBus;
        Assert.NotSame(oldEvents, newEvents);
        services.Dispose();
        subscription.Dispose();
        newEvents.Publish(1);
        Assert.Equal(0, oldCalls);
        Assert.Same(newEvents, GameServices.EventBus);
        Assert.Same(current, GameServices.Net);
        GameServices.Net.Send(new Login());
        GameServices.Net.Tick(0);
    }

    /// <summary>验证 game-services：网络提前释放不改变注册状态，业务操作负责拒绝已释放实例。</summary>
    [Fact]
    public void ExternallyDisposedNetworkKeepsRegistrationUntilOwnerDisposes()
    {
        using var eventBus = new EventBusService();
        var net = new NetService(new DoubleNetTransport(), eventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var services = Register(net, eventBus);
        Assert.Same(eventBus, GameServices.EventBus);
        net.Dispose();
        Assert.Same(eventBus, GameServices.EventBus);
        var calls = 0;
        using var subscription = GameServices.EventBus.Subscribe<int>(_ => calls++);
        GameServices.EventBus.Publish(1);
        Assert.Equal(1, calls);
        Assert.True(GameServices.IsInitialized);
        Assert.Same(net, GameServices.Net);
        Assert.Throws<ObjectDisposedException>(() => GameServices.Net.Send(new Login()));
        using var other = new NetService(new DoubleNetTransport(), eventBus, new Rpg.Services.LogService(Console.WriteLine));
        Assert.Throws<InvalidOperationException>(() => Register(other, eventBus));
    }

    /// <summary>验证 game-services：事件入口与其他入口统一返回注册实例，已释放操作由服务拒绝。</summary>
    [Fact]
    public void ExternallyDisposedEventsKeepRegistrationUntilOwnerDisposes()
    {
        using var lifetime = Register(new DoubleNetTransport());
        var eventBus = GameServices.EventBus;
        var net = GameServices.Net;
        eventBus.Dispose();
        Assert.True(GameServices.IsInitialized);
        Assert.Same(eventBus, GameServices.EventBus);
        Assert.Same(net, GameServices.Net);
        Assert.Throws<ObjectDisposedException>(() => GameServices.EventBus.Subscribe<int>(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => GameServices.EventBus.Publish(1));
        lifetime.Dispose();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.EventBus);
    }

    /// <summary>传输释放异常也必须撤销入口，避免阻止后续启动。</summary>
    [Fact]
    public void DisposalFailureStillClearsGlobalAccess()
    {
        using var eventBus = new EventBusService();
        var transport = new DoubleLifetimeTransport { FailDispose = true };
        var net = new NetService(transport, eventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var services = Register(net, eventBus);
        Assert.Throws<IOException>(() => services.Dispose());
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        Assert.Throws<InvalidOperationException>(() => GameServices.EventBus);
        services.Dispose();
        Assert.Equal(1, transport.DisposeCount);
        using var current = Register(new DoubleNetTransport());
        Assert.True(GameServices.IsInitialized);
    }

    /// <summary>验证 game-services：显式网络使用各自请求队列，统一借用全局事件域。</summary>
    [Fact]
    public void ExplicitNetworksKeepRequestQueuesAndShareEventBusService()
    {
        using var eventBus = new EventBusService();
        Assert.Throws<InvalidOperationException>(() => new SessionService(GameServices.Net));
        using var globalTransport = new DoubleNetTransport();
        using var services = Register(new NetService(globalTransport, eventBus, new Rpg.Services.LogService(Console.WriteLine)), eventBus);
        Assert.Same(eventBus, GameServices.EventBus);
        using var globalSession = new SessionService(GameServices.Net);
        using var isolatedTransport = new DoubleNetTransport();
        using var isolated = new NetService(isolatedTransport, GameServices.EventBus, new Rpg.Services.LogService(Console.WriteLine));
        using var isolatedSession = new SessionService(isolated);
        globalSession.Login("demo1", "123");
        isolatedSession.Login("demo2", "123");
        GameServices.Net.Tick(0);
        Assert.Empty(isolatedTransport.Sent);
        isolated.Tick(0);
        var globalRequest = Assert.IsType<Login>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(globalTransport.Sent)), true));
        var isolatedRequest = Assert.IsType<Login>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(isolatedTransport.Sent)), true));
        Assert.Equal("demo1", globalRequest.Username);
        Assert.Equal("demo2", isolatedRequest.Username);
        Assert.Same(eventBus, GameServices.EventBus);
    }

    /// <summary>新旧连接的代次可能相同，旧会话也不能在重启后修改新连接或取消其请求。</summary>
    [Fact]
    public void OldGlobalSessionCannotOperateOrResetRestartedNetwork()
    {
        using var oldServices = Register(new DoubleNetTransport());
        using var oldSession = new SessionService(GameServices.Net);
        oldServices.Dispose();
        using var transport = new DoubleNetTransport();
        using var currentServices = Register(transport);
        var currentEvents = GameServices.EventBus;
        using var currentSession = new SessionService(GameServices.Net);
        currentSession.Login("demo2", "123");
        Assert.Throws<ObjectDisposedException>(() => oldSession.SelectRole(1001));
        Assert.Throws<ObjectDisposedException>(() => oldSession.LeaveRole());
        Assert.Throws<ObjectDisposedException>(() => oldSession.SubmitMove(1, Godot.Vector2.Zero));
        oldSession.Dispose();
        oldServices.Dispose();
        GameServices.Net.Tick(0);
        var request = Assert.IsType<Login>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(transport.Sent)), true));
        Assert.Equal("demo2", request.Username);
        Assert.Same(currentEvents, GameServices.EventBus);
    }

    /// <summary>显式创建独立网络与会话不改变已注册服务和其消息队列。</summary>
    [Fact]
    public void ExplicitInstancesRemainIndependentOfGlobalRegistration()
    {
        using var eventBus = new EventBusService();
        using var globalTransport = new DoubleNetTransport();
        using var services = Register(new NetService(globalTransport, eventBus, new Rpg.Services.LogService(Console.WriteLine)), eventBus);
        var globalNet = GameServices.Net;
        GameServices.Net.Send(new Login());
        using (var isolatedTransport = new DoubleNetTransport())
        using (var isolated = new NetService(isolatedTransport, GameServices.EventBus, new Rpg.Services.LogService(Console.WriteLine)))
        using (var session = new SessionService(isolated))
        {
            session.Login("demo2", "123");
            isolated.Tick(0);
            Assert.Single(isolatedTransport.Sent);
            Assert.Empty(globalTransport.Sent);
        }
        Assert.Same(globalNet, GameServices.Net);
        GameServices.Net.Tick(0);
        Assert.Single(globalTransport.Sent);
    }

    /// <summary>验证 session-events：多个网络向唯一事件服务分发，监听错误不再依赖网络错误通道。</summary>
    [Fact]
    public void NetworksPublishToSharedServiceWithoutOwningItsErrorsOrLifetime()
    {
        using var eventBus = new EventBusService();
        using var firstTransport = new DoubleNetTransport();
        using var services = Register(new NetService(firstTransport, eventBus, new Rpg.Services.LogService(Console.WriteLine)), eventBus);
        Assert.Same(eventBus, GameServices.EventBus);
        using var secondTransport = new DoubleNetTransport();
        using var secondNet = new NetService(secondTransport, GameServices.EventBus, new Rpg.Services.LogService(Console.WriteLine));
        var calls = 0;
        var eventErrors = new List<Exception>();
        var netErrors = new List<Exception>();
        eventBus.Error += eventErrors.Add;
        GameServices.Net.Error += netErrors.Add;
        secondNet.Error += netErrors.Add;
        using var failing = eventBus.Subscribe<LoginRt>(_ => throw new IOException("监听失败"));
        using var observer = eventBus.Subscribe<LoginRt>(_ => calls++);
        firstTransport.Inject(MessageCodec.Encode(new LoginRt(), 1, 1, false));
        secondTransport.Inject(MessageCodec.Encode(new LoginRt(), 1, 1, false));
        GameServices.Net.Tick(0);
        secondNet.Tick(0);
        Assert.Equal(2, calls);
        Assert.Equal(2, eventErrors.Count);
        Assert.Empty(netErrors);
        secondNet.Dispose();
        Assert.Same(eventBus, GameServices.EventBus);
        eventBus.Publish(new LoginRt());
        Assert.Equal(3, calls);
    }

    /// <summary>验证 session-events 的唯一创建边界，禁止运行时消费者重新引入私有事件系统。</summary>
    [Fact]
    public void EventBusServiceHasNoPublicConstructorAndOnlyGameServicesCreatesIt()
    {
        using var eventBus = new EventBusService();
        Assert.Empty(typeof(EventBusService).GetConstructors());
        Assert.Null(typeof(NetService).GetProperty("EventBus"));
        var source = Path.Combine(ConfigTestFixture.FindRoot(), "src");
        var creators = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"new\s+EventBusService\s*\("))
            .Select(path => Path.GetRelativePath(source, path).Replace('\\', '/')).ToArray();
        Assert.Equal(new[] { "Services/GameServices.cs" }, creators);
        Assert.DoesNotContain(Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains("EventSystem"));
    }

    /// <summary>验证 game-services 的共享释放状态契约，所有真实服务均可通过接口查询且重复释放幂等。</summary>
    [Fact]
    public void AllServicesExposeDisposalStateThroughCommonContract()
    {
        using var lifetime = Register(new DoubleNetTransport());
        IService[] services = [GameServices.Env, GameServices.Log, GameServices.Config, GameServices.Setting, GameServices.EventBus,
            GameServices.MockServer, GameServices.Net, GameServices.Session, GameServices.World, GameServices.Animation, GameServices.Items];
        Assert.All(services, service => Assert.False(service.IsDisposed));
        lifetime.Dispose();
        Assert.All(services, service => Assert.True(service.IsDisposed));
        foreach (var service in services) service.Dispose();
        Assert.All(services, service => Assert.True(service.IsDisposed));
    }

    /// <summary>为重启验收从传输构建独立的事件与网络实例，成功后统一接管。</summary>
    private static IDisposable Register(INetTransport transport)
    {
        var eventBus = new EventBusService();
        NetService? net = null;
        try
        {
            net = new NetService(transport, eventBus, new Rpg.Services.LogService(Console.WriteLine));
            return Register(net, eventBus);
        }
        catch { net?.Dispose(); eventBus.Dispose(); throw; }
    }

    /// <summary>创建完整服务组，注册失败时释放仍归调用方的配置和设置。</summary>
    private static IDisposable Register(NetService net, EventBusService eventBus)
    {
        var env = ConfigTestFixture.CreateEnv(Path.Combine(Path.GetTempPath(), "rpg-services-" + Guid.NewGuid()));
        var config = new ConfigService(env);
        var setting = new SettingService(env, new Rpg.Services.LogService(Console.WriteLine));
        try { return GameServicesTestFactory.Register(env, eventBus, config, setting, net); }
        catch { setting.Dispose(); config.Dispose(); env.Dispose(); throw; }
    }

    /// <summary>服务于 game-services 释放契约的计数传输，可模拟关闭失败。</summary>
    private sealed class DoubleLifetimeTransport : INetTransport
    {
        /// <summary>底层释放被调用的次数。</summary>
        public int DisposeCount { get; private set; }
        /// <summary>是否在关闭时模拟系统错误。</summary>
        public bool FailDispose { get; init; }
        /// <summary>本替身不生成响应，因此无需保存接收监听。</summary>
        public event Action<byte[]>? Received { add { } remove { } }
        /// <summary>本替身只验证生命周期，不保存请求。</summary>
        public void Send(byte[] packet) { }
        /// <summary>不生成延迟消息。</summary>
        public void Tick(double delta) { }
        /// <summary>不保存会话状态。</summary>
        public void ResetSession(long generation) { }
        /// <summary>记录释放并取消接收监听，可模拟关闭异常。</summary>
        public void Dispose()
        {
            DisposeCount++;
            if (FailDispose) throw new IOException("模拟传输关闭失败");
        }
    }
}
