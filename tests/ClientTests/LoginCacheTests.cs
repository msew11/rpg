using Rpg.Net.Message;
using Rpg.Session;
using Rpg.Session.Cache;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session 的登录摘要缓存、异常响应与跨角色生命周期。</summary>
public sealed class LoginCacheTests
{
    /// <summary>早注册的普通监听读取已转换的摘要，修改协议或集合不能污染缓存。</summary>
    [Fact]
    public void LoginConvertsBeforeOrdinaryListenersAndOwnsReadOnlySummaries()
    {
        using var events = new EventSystem();
        LoginCache? cache = null;
        IReadOnlyList<RoleSummaryPbData>? observed = null;
        using var listener = events.Subscribe<LoginRt>(_ => observed = cache!.Roles);
        using var loginCache = cache = new LoginCache(events, new LoginRt { Success = true });
        var response = new LoginRt
        {
            Success = true,
            Roles = { new RoleSummaryPb { RoleId = 1001, Name = "晨星", Level = 1, Gender = RoleGenderPb.Male } }
        };
        events.Publish(response);
        Assert.True(cache.IsLoggedIn);
        Assert.Same(cache.Roles, observed);
        var summary = Assert.Single(observed!);
        response.Roles[0].Name = "篡改协议";
        response.Roles.Clear();
        Assert.Equal("晨星", summary.Name);
        var list = Assert.IsAssignableFrom<IList<RoleSummaryPbData>>(cache.Roles);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Same(summary, Assert.Single(cache.Roles));
    }

    /// <summary>失败和非法摘要均清除旧账号数据，非法成功响应不交付普通监听且不留下半份列表。</summary>
    [Fact]
    public void FailedAndMalformedResponsesClearPreviousAccountAndAllowRetry()
    {
        using var fixture = new NetworkTestFixture();
        Assert.True(fixture.Login().Success);
        var previous = fixture.LoginCache;
        Assert.False(fixture.Login("demo", "wrong").Success);
        Assert.Null(fixture.Session);
        Assert.False(previous.IsLoggedIn);
        Assert.Empty(previous.Roles);
        Assert.True(fixture.Login().Success);
        var ordinaryCalls = 0;
        using var listener = fixture.Net.Events.Subscribe<LoginRt>(_ => ordinaryCalls++);
        fixture.Net.Events.Publish(new LoginRt
        {
            Success = true,
            Roles =
            {
                new RoleSummaryPb { RoleId = 1001, Name = "晨星", Level = 1, Gender = RoleGenderPb.Male },
                new RoleSummaryPb { RoleId = 1002, Name = "无效摘要", Level = 0, Gender = RoleGenderPb.Female }
            }
        });
        Assert.False(fixture.LoginCache.IsLoggedIn);
        Assert.Empty(fixture.LoginCache.Roles);
        Assert.Equal(0, ordinaryCalls);
        Assert.IsType<InvalidDataException>(Assert.Single(fixture.Errors));
        Assert.True(fixture.Login().Success);
        Assert.True(fixture.LoginCache.IsLoggedIn);
        Assert.Equal(2, fixture.LoginCache.Roles.Count);
        Assert.Equal(1, ordinaryCalls);
    }

    /// <summary>离开角色保留账号会话与摘要，退出账号清空缓存，重登创建新会话。</summary>
    [Fact]
    public void LoginCacheSurvivesRoleSessionsUntilClearedOrDisposed()
    {
        using var fixture = new NetworkTestFixture();
        var login = fixture.Login();
        var account = fixture.Session!;
        var roles = fixture.LoginCache.Roles;
        fixture.SelectRole(login.Roles[0].RoleId).LeaveRole();
        Assert.True(fixture.LoginCache.IsLoggedIn);
        Assert.Same(roles, fixture.LoginCache.Roles);
        fixture.SelectRole(login.Roles[1].RoleId).LeaveRole();
        Assert.Same(account, fixture.Session);
        Assert.Same(roles, fixture.LoginCache.Roles);
        account.Dispose();
        Assert.False(account.LoginCache.IsLoggedIn);
        Assert.Empty(account.LoginCache.Roles);
        Assert.False(account.RoleCache.IsInitialized);
        Assert.Equal(2, roles.Count);
        Assert.True(fixture.Login().Success);
        Assert.NotSame(account, fixture.Session);
        fixture.Session!.Dispose();
        fixture.Session.Dispose();
        fixture.Net.Events.Publish(new LoginRt { Success = true });
        Assert.False(fixture.LoginCache.IsLoggedIn);
        Assert.Empty(fixture.LoginCache.Roles);
        Assert.Empty(fixture.Errors);
    }
}
