using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Session;

namespace ClientTests;

/// <summary>通过真实字节链路和可清理存储服务 client-network、mock-server、client-session 验收。</summary>
internal sealed class NetworkTestFixture : IDisposable
{
    /// <summary>是否由此实例负责删除测试存储。</summary>
    private readonly bool _ownsDirectory;
    /// <summary>当前夹具的隔离存档目录。</summary>
    public string DirectoryPath { get; }
    /// <summary>测试可调用主动推送入口的独立服务端。</summary>
    public MockServer Server { get; }
    /// <summary>真实队列与事件网络管理器。</summary>
    public NetManager Net { get; }
    /// <summary>观察可诊断网络错误。</summary>
    public List<Exception> Errors { get; } = [];

    /// <summary>创建独立目录，或借用已有目录验证服务器重建。</summary>
    public NetworkTestFixture(string? directory = null)
    {
        _ownsDirectory = directory is null;
        DirectoryPath = directory ?? Path.Combine(Path.GetTempPath(), "rpg-network-" + Guid.NewGuid());
        Server = new MockServer(DirectoryPath);
        Net = new NetManager(new MockTransport(Server));
        Net.Error += Errors.Add;
    }

    /// <summary>提交登录并显式推进网络，将结果复制为客户端摘要。</summary>
    public DataLoginResult Login(string username = "demo", string password = "demo")
    {
        LoginRt? response = null;
        using var subscription = Net.Events.Subscribe<LoginRt>(message => response = message);
        Net.Send(new Login { Username = username, Password = password });
        Net.Tick(0);
        Assert.NotNull(response);
        return new DataLoginResult(response.Success, response.Error, response.Roles.Select(RoleMessageMapper.ToRole).ToArray());
    }

    /// <summary>在请求前建立会话监听并验证选角响应初始化缓存。</summary>
    public GameSession SelectRole(long roleId)
    {
        var session = new GameSession(roleId, Net);
        Net.Send(new SelectRole { RoleId = roleId });
        Net.Tick(0);
        Assert.True(session.Cache.Role.IsInitialized);
        return session;
    }

    /// <summary>先释放所有通信资源，再删除自己创建的隔离目录。</summary>
    public void Dispose()
    {
        Net.Dispose();
        if (_ownsDirectory && Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
    }
}
