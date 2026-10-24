using Rpg.Services;
using Godot;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Session;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session 的登录后建会话及角色代次隔离契约。</summary>
[Trait("Runtime", "Godot")]
public sealed class SessionServiceTests : RegisteredServiceTestScope
{
    /// <summary>验证 game-services：已登录账号在整组释放撤销全局入口后仍完成缓存与网络清理。</summary>
    [Fact]
    public void AccountDisposalUsesBoundDependenciesAfterRegistrationIsRevoked()
    {
        var net = GameServices.Net;
        var session = GameServices.Session;
        session.Login("demo", "demo");
        net.Tick(0);
        Assert.True(session.IsLoggedIn);
        var login = session.LoginCache;
        var generation = net.Generation;
        Dispose();
        Assert.False(GameServices.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => GameServices.Events);
        Assert.True(session.IsDisposed);
        Assert.False(login.IsLoggedIn);
        Assert.True(net.Generation > generation);
        Assert.True(net.IsDisposed);
        Assert.True(Events.IsDisposed);
    }

    /// <summary>验证 client-session 与 session-events：空缓存拒绝选角，重试及退出回收旧监听。</summary>
    [Fact]
    public void LoginAttemptsOwnEmptyCachesAndReleasePreviousSubscriptions()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, Events);
        using var session = new SessionService(net);
        session.Login("demo", "wrong");
        var previous = session.LoginCache;
        Assert.False(session.IsLoggedIn);
        Assert.Throws<InvalidOperationException>(() => session.SelectRole(1001));
        Assert.Throws<InvalidOperationException>(() => session.Role);
        Events.Publish(new LoginRt { Error = "凭据错误" });
        Assert.False(session.IsLoggedIn);
        session.Login("demo", "demo");
        var current = session.LoginCache;
        Assert.NotSame(previous, current);
        Events.Publish(new LoginRt { Success = true });
        Assert.True(session.IsLoggedIn);
        Assert.False(previous.IsLoggedIn);
        session.Logout();
        Events.Publish(new LoginRt { Success = true });
        Assert.False(current.IsLoggedIn);
        Assert.False(session.IsLoggedIn);
    }

    /// <summary>失败或损坏的初始登录不创建会话、不遗留订阅，也不改变已有网络请求。</summary>
    [Fact]
    public void InvalidInitialLoginDoesNotCreateSessionOrLeaveSubscriptions()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, Events);
        net.Send(new Login { Username = "demo", Password = "demo" });
        Assert.Throws<InvalidOperationException>(() => new SessionService(net, new LoginRt { Error = "登录失败" }));
        var invalid = new LoginRt { Success = true, Roles = { new RoleSummaryPb { RoleId = 1001 } } };
        Assert.Throws<InvalidDataException>(() => new SessionService(net, invalid));
        var errors = new List<Exception>();
        Events.Error += errors.Add;
        Events.Publish(invalid);
        Assert.Empty(errors);
        net.Tick(0);
        Assert.Single(transport.Sent);
    }

    /// <summary>同账号重新选择同角色后，旧代次的已编码移动与推送都不能更新缓存。</summary>
    [Fact]
    public void ReenteringSameRoleRejectsOldPacketsWhileKeepingLoginCache()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, Events);
        using var session = new SessionService(net, new LoginRt { Success = true });
        var loginCache = session.LoginCache;
        var state = new RoleWorldStatePb
        {
            RoleId = 1001, Name = "晨星", Level = 1, Health = 80, MaxHealth = 100,
            MaxMana = 10, NextExperience = 20, X = 100, Y = 200, Gender = RoleGenderPb.Male
        };
        session.SelectRole(1001);
        net.Tick(0);
        var oldGeneration = MessageCodec.ReadEnvelope(transport.Sent[^1]).Generation;
        transport.Inject(MessageCodec.Encode(new SelectRoleRt { Success = true, RoleId = 1001, State = state }, oldGeneration, false));
        net.Tick(0);
        session.LeaveRole();
        session.SelectRole(1001);
        Assert.False(session.RoleCache.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => session.SubmitMove(1, Vector2.Zero));
        net.Tick(0);
        var generation = MessageCodec.ReadEnvelope(transport.Sent[^1]).Generation;
        Assert.True(generation > oldGeneration);
        transport.Inject(MessageCodec.Encode(new SelectRoleRt { Success = true, RoleId = 1001, State = state }, generation, false));
        var staleState = state.Clone();
        staleState.Revision = 99;
        staleState.Health = 1;
        staleState.AcknowledgedAction = 7;
        transport.Inject(MessageCodec.Encode(new MoveRt { Success = true, RoleId = 1001, ActionId = 7, State = staleState }, oldGeneration, false));
        transport.Inject(MessageCodec.Encode(new RoleStateNotice { State = staleState }, oldGeneration, false));
        var calls = 0;
        using var moved = Events.Subscribe<MoveRt>(_ => calls++);
        using var noticed = Events.Subscribe<RoleStateNotice>(_ => calls++);
        net.Tick(0);
        Assert.Same(loginCache, session.LoginCache);
        Assert.Equal(80, session.Role.Health);
        Assert.Equal(0, session.RoleCache.State.Revision);
        Assert.Equal(0, calls);
    }

    /// <summary>旧账号会话被替换后不能操作网络，晚释放也不会取消新会话的待选角请求。</summary>
    [Fact]
    public void DisposingReplacedAccountDoesNotResetNewSession()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, Events);
        using var previous = new SessionService(net, new LoginRt { Success = true });
        using var current = new SessionService(net, new LoginRt { Success = true });
        Assert.Throws<InvalidOperationException>(() => previous.SelectRole(1001));
        current.SelectRole(1002);
        previous.Dispose();
        Assert.Throws<ObjectDisposedException>(() => previous.SelectRole(1001));
        net.Tick(0);
        var request = Assert.IsType<SelectRole>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(Assert.Single(transport.Sent)), true));
        Assert.Equal(1002, request.RoleId);
    }
}
