using Rpg.Config;
using Rpg.Net;
using Rpg.Services;
using Rpg.Session;
using Rpg.Session.Cache;
using Rpg.Session.Data;
using Rpg.Server.Hosting;

namespace ClientTests;

/// <summary>为 client-actor-presentation 验收提供正式异步字节与角色缓存。</summary>
internal sealed class NetworkTestFixture : IDisposable
{
    /// <summary>此夹具拥有的存档根。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-actor-network-" + Guid.NewGuid());
    /// <summary>独立服务器所借用的环境。</summary>
    private readonly EnvService _env;
    /// <summary>此夹具拥有的服务器。</summary>
    private readonly MockServerService _server;
    /// <summary>独立的正式网络。</summary>
    public NetService Net { get; }
    /// <summary>账号和角色缓存。</summary>
    public SessionService Session { get; }
    /// <summary>准备存档并借用外层已注册的配置和事件域。</summary>
    internal NetworkTestFixture(EventBusService events, bool seedRoles = false)
    {
        if (seedRoles) Rpg.Tests.RoleTestData.Seed(_directory);
        _env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        _server = MockServerService.CreateInProcess(_env, GameServices.Config);
        Net = NetService.CreateInProcess(_server, events, GameServices.Log);
        Session = new SessionService(Net);
    }
    /// <summary>认证后返回不可变摘要。</summary>
    public (bool Success, string Error, IReadOnlyList<RoleSummaryPbData> Roles) Login(string username = "demo1", string password = "123")
    {
        Session.Login(username, password);
        PumpUntil(() => !Session.AccountRequestPending);
        return (Session.IsLoggedIn, "", Session.Roles);
    }
    /// <summary>等完整进入响应接纳后返回角色会话。</summary>
    public SessionService SelectRole(long roleId)
    {
        Session.SelectRole(roleId);
        PumpUntil(() => !Session.AccountRequestPending);
        Assert.True(Session.HasRole);
        return Session;
    }
    /// <summary>异步回复只由客户端固定帧发布。</summary>
    private void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { Net.Tick(0); Thread.Sleep(1); }
        Assert.True(done());
    }
    /// <summary>先结束缓存和连接，再释放独立服务器。</summary>
    public void Dispose()
    {
        Session.Dispose(); Net.Dispose(); _server.Dispose(); _env.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
