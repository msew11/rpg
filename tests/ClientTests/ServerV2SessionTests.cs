using Rpg.Const;
using Rpg.Net;
using Rpg.ProtocolV2;
using Rpg.Services;
using Rpg.Tests;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-account 的客户端缓存优先、在途限制与失败原子性。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2SessionTests : IDisposable
{
    /// <summary>每例服务组拥有独立数据目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-v2-session-" + Guid.NewGuid());

    /// <summary>同一客户端只发一个账号操作，普通监听读取已完成的不可变缓存。</summary>
    [Fact]
    public void AccountOperationsUseV2AndCacheBeforeOrdinaryListeners()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        var loggedIn = false;
        using var login = GameServices.EventBus.Subscribe<LoginRt>(_ => loggedIn = session.IsLoggedIn);
        session.Login(" DEMO ", "demo");
        Assert.True(session.AccountRequestPending);
        Assert.Throws<InvalidOperationException>(() => session.Login("demo", "demo"));
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(loggedIn);
        var createdInCache = false;
        using var created = GameServices.EventBus.Subscribe<CreateRoleRt>(reply =>
        {
            createdInCache = session.Roles.Count == reply.Roles.Count;
            reply.Roles[0].Name = "不能修改缓存";
        });
        session.CreateRole("缓存角色", RoleGender.Female, 17);
        Assert.Throws<InvalidOperationException>(() => session.DeleteRole(1003));
        Assert.Throws<InvalidOperationException>(() => session.CreateRole("重复提交", RoleGender.Male, 9));
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(createdInCache);
        var summary = Assert.Single(session.Roles);
        Assert.Equal("缓存角色", summary.Name);
        Assert.Equal(summary.ToV2Pb(), Rpg.Session.Data.RoleSummaryPbData.FromPb(summary.ToV2Pb()).ToV2Pb());
        Assert.Throws<NotSupportedException>(() => session.SelectRole(summary.RoleId));
        Assert.False(GameServices.World.HasWorld);
        Assert.False(session.HasRole);
        session.DeleteRole(summary.RoleId);
        PumpUntil(() => !session.AccountRequestPending);
        Assert.Empty(session.Roles);
    }

    /// <summary>非法完整快照不通知 UI、不替换原列表，统一失败和网络错误都解除等待。</summary>
    [Fact]
    public void MalformedSnapshotPreservesListAndAllowsRetry()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo", "demo");
        PumpUntil(() => !session.AccountRequestPending);
        var cache = session.LoginCache;
        var notifications = 0;
        var failures = new List<Exception>();
        GameServices.EventBus.Error += failures.Add;
        using var observed = GameServices.EventBus.Subscribe<CreateRoleRt>(_ => notifications++);
        var valid = new RoleSummaryPb { RoleId = 42, Name = "完整摘要", Level = 1, Gender = RoleGenderPb.Male, HeadId = 9 };
        cache.BeginV2Request<CreateRoleRt>();
        GameServices.EventBus.Publish(new CreateRoleRt { Roles = { valid } });
        var previous = session.Roles;
        var badGender = valid.Clone();
        badGender.Gender = RoleGenderPb.Unspecified;
        var badLevel = valid.Clone();
        badLevel.Level = 0;
        foreach (var reply in new[] { new CreateRoleRt { Roles = { badGender } },
                     new CreateRoleRt { Roles = { valid, badLevel } }, new CreateRoleRt { Roles = { valid, valid.Clone() } } })
        {
            cache.BeginV2Request<CreateRoleRt>();
            GameServices.EventBus.Publish(reply);
            Assert.Same(previous, session.Roles);
            Assert.False(session.AccountRequestPending);
        }
        Assert.Equal(1, notifications);
        Assert.Equal(3, failures.Count);
        cache.BeginV2Request<DeleteRoleRt>();
        GameServices.EventBus.Publish(new ErrorRt { Code = ErrorCodePb.RoleNotFound, Message = "角色不存在" });
        Assert.Same(previous, session.Roles);
        Assert.False(session.AccountRequestPending);
        cache.BeginV2Request<DeleteRoleRt>();
        session.CancelAccountRequest();
        Assert.False(session.AccountRequestPending);
        session.Login("demo", "demo");
        GameServices.EventBus.Publish(new LoginRt { Roles = { valid, badLevel } });
        Assert.False(session.IsLoggedIn);
        Assert.False(session.AccountRequestPending);
        Assert.Empty(session.Roles);
    }

    /// <summary>返回登录推进代次并清缓存，已完成旧请求的输出也不能建立新账号。</summary>
    [Fact]
    public void LogoutDiscardsOldReplyAndRetryCreatesFreshCache()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo", "demo");
        GameServices.Net.Tick(0);
        var previous = session.LoginCache;
        session.Logout();
        for (var i = 0; i < 5; i++) GameServices.Net.Tick(0);
        Assert.False(session.IsLoggedIn);
        Assert.Empty(session.Roles);
        Assert.False(previous.IsLoggedIn);
        session.Login("demo", "wrong");
        PumpUntil(() => !session.AccountRequestPending);
        Assert.False(session.IsLoggedIn);
        session.Login("demo", "demo");
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(session.IsLoggedIn);
        Assert.NotSame(previous, session.LoginCache);
    }

    /// <summary>推进正式固定帧传输，等待后台 Actor 回复但不阻塞网络收取。</summary>
    private static void PumpUntil(Func<bool> complete)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!complete() && DateTime.UtcNow < deadline) { GameServices.Net.Tick(0); Thread.Sleep(1); }
        Assert.True(complete(), "V2 账号请求未完成");
    }
    /// <summary>仅删除测试创建的临时数据。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
