using System;
using System.Threading.Tasks;
using Rpg.Config;
using Rpg.Services;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Modules;

namespace Rpg.ServerV2.Hosting;

/// <summary>Net 拥有的服务器持有服务；构造时不创建服务器。</summary>
public sealed class MockServerService : Service, IAsyncDisposable
{
    /// <summary>借用环境与配置，仅在登录启动服务器时使用。</summary>
    private readonly EnvService _env;
    /// <summary>共享只读配置。</summary>
    public ConfigService Config { get { ThrowIfDisposed(); ObjectDisposedException.ThrowIf(_config.IsDisposed, _config); return _config; } }
    /// <summary>借用的配置实例。</summary>
    private readonly ConfigService _config;
    /// <summary>当前拥有的服务器；纯客户端与初始启动均为空。</summary>
    public MockServer? Server { get; private set; }
    /// <summary>服务器诊断事件。</summary>
    public event Action<Exception>? Error;
    /// <summary>仅保存依赖，不组装服务器。</summary>
    public MockServerService(EnvService env, ConfigService config)
    {
        ArgumentNullException.ThrowIfNull(env); ArgumentNullException.ThrowIfNull(config);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env); ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        _env = env; _config = config;
    }
    /// <summary>测试显式提供已组装模块，避免生产隐式创建服务器。</summary>
    internal MockServerService(EnvService env, ServerModules modules) : this(env, modules?.Config ?? throw new ArgumentNullException(nameof(modules))) => Attach(new MockServer(env, modules));
    /// <summary>测试显式创建进程内宿主，不占用端口。</summary>
    internal static MockServerService CreateInProcess(EnvService env, ConfigService config)
    { var service = new MockServerService(env, config); service.Attach(new MockServer(env, config)); return service; }
    /// <summary>接管已经启动或显式测试组装的实例。</summary>
    public void Attach(MockServer server)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(server);
        if (Server is not null) throw new InvalidOperationException("已有服务器实例");
        Server = server; server.Error += Report;
    }
    /// <summary>相同端口复用；新实例监听成功后才交付所有权。</summary>
    public async Task StartAsync(int port)
    {
        ThrowIfDisposed();
        if (Server?.Port == port) return;
        if (Server is not null) throw new InvalidOperationException("请先确认并停止原服务器");
        var candidate = new MockServer(_env, Config);
        try { candidate.Start(port); Attach(candidate); }
        catch { await candidate.DisposeAsync().ConfigureAwait(false); throw; }
    }
    /// <summary>关闭当前服务器，保存失败时保留引用供错误处理。</summary>
    public async Task StopAsync()
    {
        var server = Server; if (server is null) return;
        if (!server.IsDisposed) await server.FlushAsync().ConfigureAwait(false);
        await server.DisposeAsync().ConfigureAwait(false); server.Error -= Report; Server = null;
    }
    /// <summary>要求调用者已显式创建服务器。</summary>
    private MockServer Required { get { ThrowIfDisposed(); return Server ?? throw new InvalidOperationException("服务器尚未启动"); } }
    /// <summary>进程内测试适配入口。</summary>
    public DataConnectionId OpenConnection() => Required.OpenConnection();
    /// <summary>转交字节请求。</summary>
    public Task ReceiveAsync(DataConnectionId id, byte[] packet) => Required.ReceiveAsync(id, packet);
    /// <summary>转交测试会话重置。</summary>
    public ulong ResetSession(DataConnectionId id) => Required.ResetSession(id);
    /// <summary>转交测试输出读取。</summary>
    public bool TryReadOutgoing(DataConnectionId id, out byte[] packet) => Required.TryReadOutgoing(id, out packet);
    /// <summary>返回测试会话快照。</summary>
    public DataSessionSnapshot Session(DataConnectionId id) => Required.Session(id);
    /// <summary>关闭测试连接。</summary>
    public Task CloseConnectionAsync(DataConnectionId id) => Required.CloseConnectionAsync(id);
    /// <summary>提交当前服务器，不启动新实例。</summary>
    public Task FlushAsync() { ThrowIfDisposed(); return Server?.FlushAsync() ?? Task.CompletedTask; }
    /// <summary>仅无监听的测试宿主使用手动时钟。</summary>
    public void Tick(double delta) { ThrowIfDisposed(); if (Server is { Port: 0 } server) server.Tick(delta); }
    /// <summary>供周期保存验收观察。</summary>
    internal Task PeriodicFlush => Required.PeriodicFlush;
    /// <summary>同步服务释放入口。</summary>
    public override void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>释放拥有的实例，不释放借用依赖。</summary>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return; IsDisposed = true;
        try { if (Server is { } server) await server.DisposeAsync().ConfigureAwait(false); }
        finally { Server = null; Error = null; }
    }
    /// <summary>转发服务器诊断。</summary>
    private void Report(Exception error) { if (Error is { } handlers) foreach (Action<Exception> handler in handlers.GetInvocationList()) try { handler(error); } catch { } }
}
