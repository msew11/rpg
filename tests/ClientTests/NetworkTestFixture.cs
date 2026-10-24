using Rpg.Services;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Session;
using Rpg.Session.Cache;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>通过真实字节链路和可清理存储服务 client-network、mock-server、client-session 验收。</summary>
internal sealed class NetworkTestFixture : IDisposable
{
    /// <summary>是否由此实例负责删除测试存储。</summary>
    private readonly bool _ownsDirectory;
    /// <summary>本夹具借用的事件域。</summary>
    private readonly EventBusService _eventBus;
    /// <summary>当前夹具的隔离存档目录。</summary>
    public string DirectoryPath { get; }
    /// <summary>测试可调用主动推送入口的独立服务端。</summary>
    public MockServer Server { get; }
    /// <summary>真实队列与事件网络服务。</summary>
    public NetService Net { get; }
    /// <summary>登录成功后建立的账号会话，未登录或失败时为空。</summary>
    public SessionService? Session { get; private set; }
    /// <summary>当前账号会话持有的登录摘要缓存。</summary>
    public LoginCache LoginCache => Session!.LoginCache;
    /// <summary>观察可诊断网络错误。</summary>
    public List<Exception> Errors { get; } = [];

    /// <summary>创建独立目录，或借用已有目录验证服务器重建。</summary>
    public NetworkTestFixture(EventBusService eventBus, string? directory = null)
    {
        _eventBus = eventBus;
        _ownsDirectory = directory is null;
        DirectoryPath = directory ?? Path.Combine(Path.GetTempPath(), "rpg-network-" + Guid.NewGuid());
        Server = new MockServer(DirectoryPath);
        Net = new NetService(new MockTransport(Server), eventBus);
        Net.Error += Errors.Add;
        _eventBus.Error += Errors.Add;
    }

    /// <summary>结束旧账号后提交登录，成功才创建会话并从缓存读取摘要。</summary>
    public (bool Success, string Error, IReadOnlyList<RoleSummaryPbData> Roles) Login(string username = "demo", string password = "demo")
    {
        Session?.Dispose();
        Session = null;
        Net.ResetSession();
        LoginRt? response = null;
        using var subscription = _eventBus.Subscribe<LoginRt>(message => response = message);
        Net.Send(new Login { Username = username, Password = password });
        Net.Tick(0);
        Assert.NotNull(response);
        if (response.Success) Session = new SessionService(Net, response);
        return (response.Success, response.Error, Session?.LoginCache.Roles ?? []);
    }

    /// <summary>通过已有账号会话选角并验证响应初始化角色缓存。</summary>
    public SessionService SelectRole(long roleId)
    {
        var session = Session ?? throw new InvalidOperationException("测试应先成功登录");
        session.SelectRole(roleId);
        Net.Tick(0);
        Assert.True(session.RoleCache.IsInitialized);
        return session;
    }

    /// <summary>先释放所有通信资源，再删除自己创建的隔离目录。</summary>
    public void Dispose()
    {
        Session?.Dispose();
        _eventBus.Error -= Errors.Add;
        Net.Dispose();
        if (_ownsDirectory && Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
    }
}
