using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session;

namespace ClientTests;

/// <summary>验证 openspec/specs/game-services 的全局访问、所有权及独立实例契约；同类测试串行执行。</summary>
public sealed class GameServicesTests
{
    /// <summary>注册前明确报错，注册后发送和监听仍遵循 client-network 的固定帧队列。</summary>
    [Fact]
    public void GlobalAccessRequiresRegistrationAndKeepsQueuedDelivery()
    {
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        using var transport = new DoubleNetTransport();
        var net = new NetService(transport);
        using var services = GameServices.Initialize(net);
        Assert.True(GameServices.IsInitialized);
        Assert.Same(net, GameServices.Net);
        var delivered = 0;
        using var subscription = GameServices.Net.Events.Subscribe<LoginRt>(_ => delivered++);
        GameServices.Net.Send(new Login { Username = "demo" });
        Assert.Empty(transport.Sent);
        GameServices.Net.Tick(0);
        var generation = MessageCodec.ReadEnvelope(Assert.Single(transport.Sent)).Generation;
        transport.Inject(MessageCodec.Encode(new LoginRt { Success = true }, generation, false));
        Assert.Equal(0, delivered);
        GameServices.Net.Tick(0);
        Assert.Equal(1, delivered);
    }

    /// <summary>空对象和已释放实例不可注册，重复注册不替换或接管另一有效实例。</summary>
    [Fact]
    public void InvalidAndDuplicateRegistrationLeaveOwnershipUnchanged()
    {
        Assert.Throws<ArgumentNullException>(() => GameServices.Initialize(null!));
        using var disposed = new NetService(new DoubleNetTransport());
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => GameServices.Initialize(disposed));
        Assert.False(GameServices.IsInitialized);
        var original = new NetService(new DoubleNetTransport());
        using var services = GameServices.Initialize(original);
        using var otherTransport = new DoubleNetTransport();
        using var other = new NetService(otherTransport);
        Assert.Throws<InvalidOperationException>(() => GameServices.Initialize(other));
        Assert.Same(original, GameServices.Net);
        other.Send(new Login());
        other.Tick(0);
        Assert.Single(otherTransport.Sent);
    }

    /// <summary>句柄释放服务及监听，重建后旧句柄的再次释放不影响新服务。</summary>
    [Fact]
    public void LifetimeDisposesNetworkAndCannotReleaseLaterRegistration()
    {
        var transport = new DoubleLifetimeTransport();
        var net = new NetService(transport);
        using var services = GameServices.Initialize(net);
        using var subscription = net.Events.Subscribe<LoginRt>(_ => { });
        services.Dispose();
        services.Dispose();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        Assert.Throws<ObjectDisposedException>(() => net.Send(new Login()));
        Assert.Throws<ObjectDisposedException>(() => net.Events.Subscribe<LoginRt>(_ => { }));
        Assert.Equal(1, transport.DisposeCount);
        var current = new NetService(new DoubleNetTransport());
        using var currentServices = GameServices.Initialize(current);
        services.Dispose();
        Assert.Same(current, GameServices.Net);
        GameServices.Net.Send(new Login());
        GameServices.Net.Tick(0);
    }

    /// <summary>借用者提前释放网络时入口报错，注册所有者仍需释放句柄才能再次启动。</summary>
    [Fact]
    public void ExternallyDisposedNetworkIsNeverReturnedAsActive()
    {
        var net = new NetService(new DoubleNetTransport());
        using var services = GameServices.Initialize(net);
        net.Dispose();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<ObjectDisposedException>(() => GameServices.Net);
        using var other = new NetService(new DoubleNetTransport());
        Assert.Throws<InvalidOperationException>(() => GameServices.Initialize(other));
    }

    /// <summary>传输释放异常也必须撤销入口，避免阻止后续启动。</summary>
    [Fact]
    public void DisposalFailureStillClearsGlobalAccess()
    {
        var transport = new DoubleLifetimeTransport { FailDispose = true };
        var net = new NetService(transport);
        using var services = GameServices.Initialize(net);
        Assert.Throws<IOException>(() => services.Dispose());
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Net);
        Assert.Throws<ObjectDisposedException>(() => net.Events.Subscribe<LoginRt>(_ => { }));
        services.Dispose();
        Assert.Equal(1, transport.DisposeCount);
        using var current = GameServices.Initialize(new NetService(new DoubleNetTransport()));
        Assert.True(GameServices.IsInitialized);
    }

    /// <summary>全局会话绑定注册实例，显式会话仍可独立操作且不改变全局请求。</summary>
    [Fact]
    public void GlobalSessionAndExplicitSessionKeepSeparateNetworks()
    {
        Assert.Throws<InvalidOperationException>(() => new GameSession(new LoginRt { Success = true }));
        using var globalTransport = new DoubleNetTransport();
        using var services = GameServices.Initialize(new NetService(globalTransport));
        using var globalSession = new GameSession(new LoginRt { Success = true });
        using var isolatedTransport = new DoubleNetTransport();
        using var isolated = new NetService(isolatedTransport);
        using var isolatedSession = new GameSession(isolated, new LoginRt { Success = true });
        globalSession.SelectRole(1001);
        isolatedSession.SelectRole(1002);
        GameServices.Net.Tick(0);
        Assert.Empty(isolatedTransport.Sent);
        isolated.Tick(0);
        var globalRequest = Assert.IsType<SelectRole>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(globalTransport.Sent)), true));
        var isolatedRequest = Assert.IsType<SelectRole>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(isolatedTransport.Sent)), true));
        Assert.Equal(1001, globalRequest.RoleId);
        Assert.Equal(1002, isolatedRequest.RoleId);
        Assert.Same(GameServices.Net.Events, globalSession.Events);
        Assert.NotSame(GameServices.Net.Events, isolatedSession.Events);
    }

    /// <summary>新旧连接的代次可能相同，旧会话也不能在重启后修改新连接或取消其请求。</summary>
    [Fact]
    public void OldGlobalSessionCannotOperateOrResetRestartedNetwork()
    {
        using var oldServices = GameServices.Initialize(new NetService(new DoubleNetTransport()));
        using var oldSession = new GameSession(new LoginRt { Success = true });
        oldServices.Dispose();
        using var transport = new DoubleNetTransport();
        using var currentServices = GameServices.Initialize(new NetService(transport));
        using var currentSession = new GameSession(new LoginRt { Success = true });
        currentSession.SelectRole(1002);
        Assert.Throws<ObjectDisposedException>(() => oldSession.SelectRole(1001));
        Assert.Throws<ObjectDisposedException>(() => oldSession.LeaveRole());
        Assert.Throws<ObjectDisposedException>(() => oldSession.SubmitMove(1, Godot.Vector2.Zero));
        oldSession.Dispose();
        oldServices.Dispose();
        GameServices.Net.Tick(0);
        var request = Assert.IsType<SelectRole>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(Assert.Single(transport.Sent)), true));
        Assert.Equal(1002, request.RoleId);
        Assert.Same(GameServices.Net.Events, currentSession.Events);
    }

    /// <summary>显式创建独立网络与会话不改变已注册服务和其消息队列。</summary>
    [Fact]
    public void ExplicitInstancesRemainIndependentOfGlobalRegistration()
    {
        using var globalTransport = new DoubleNetTransport();
        using var services = GameServices.Initialize(new NetService(globalTransport));
        var globalNet = GameServices.Net;
        GameServices.Net.Send(new Login());
        using (var isolatedTransport = new DoubleNetTransport())
        using (var isolated = new NetService(isolatedTransport))
        using (var session = new GameSession(isolated, new LoginRt { Success = true }))
        {
            session.SelectRole(1002);
            isolated.Tick(0);
            Assert.Single(isolatedTransport.Sent);
            Assert.Empty(globalTransport.Sent);
        }
        Assert.Same(globalNet, GameServices.Net);
        GameServices.Net.Tick(0);
        Assert.Single(globalTransport.Sent);
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
