using Rpg.Services;
using Rpg.Tests;

namespace ClientTests;

/// <summary>为 session-events 的缓存验收注册完整服务组，结束后释放并删除隔离存储。</summary>
public abstract class RegisteredServiceTestScope : IDisposable
{
    /// <summary>本次测试拥有的临时存储根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-cache-scope-" + Guid.NewGuid());
    /// <summary>本次测试拥有的整组服务释放句柄。</summary>
    private readonly IDisposable _registration;
    /// <summary>本次注册的事件域，供测试分发消息和观察清理结果。</summary>
    protected EventService Events { get; }

    /// <summary>在缓存创建前完成注册，复用正式配置加载与测试组装流程。</summary>
    protected RegisteredServiceTestScope()
    {
        var environment = ConfigTestFixture.CreateEnvironment(Path.Combine(_directory, "settings"),
            Path.Combine(_directory, "server"));
        _registration = GameServicesTestFactory.Register(environment);
        Events = GameServices.Events;
    }

    /// <summary>撤销注册并完成逆序清理，即使清理失败也删除临时存储。</summary>
    public void Dispose()
    {
        try { _registration.Dispose(); }
        finally { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
