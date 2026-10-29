using System.IO;
using System.Threading.Tasks;
using System;
using Rpg.Config;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Login.Handler;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules.World.Storage;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Runtime;
using Rpg.Services;

namespace Rpg.ServerV2.Hosting;

/// <summary>Net 拥有的进程内服务端子服务，持有配置并装配 V2 框架。</summary>
public sealed class MockServerService : Service, IAsyncDisposable
{
    /// <summary>借用先于本服务创建、后于本服务释放的配置。</summary>
    private readonly ConfigService _config;
    /// <summary>借用环境路径与动态保存间隔，生命周期长于本服务。</summary>
    private readonly EnvService _env;
    /// <summary>本服务拥有的纯 .NET 运行实例。</summary>
    private readonly ServerRuntime _runtime;
    /// <summary>并发释放共享的停止任务。</summary>
    private readonly object _disposeGate = new();
    /// <summary>非空后不再接纳外部工作。</summary>
    private Task? _disposing;
    /// <summary>当前唯一周期保存任务；异常由周期入口报告。</summary>
    private Task _periodicFlush = Task.CompletedTask;
    /// <summary>宿主验收观察当前周期轮次完成，不触发新的保存。</summary>
    internal Task PeriodicFlush { get { lock (_disposeGate) return _periodicFlush; } }
    /// <summary>上次观察到的间隔，改变时重新计时。</summary>
    private int _lastInterval = -1;
    /// <summary>尚未到保存节点的累计帧时间。</summary>
    private double _flushElapsed;
    /// <summary>后续业务装配可使用同一个配置实例。</summary>
    public ConfigService Config { get { EnsureActive(); return _config; } }
    /// <summary>意外处理失败与 Actor 通知错误的诊断，不包含完整请求载荷。</summary>
    public event Action<Exception>? Error;

    /// <summary>公开构造只声明服务依赖，默认装配演示账号业务与内部只读查询。</summary>
    public MockServerService(EnvService env, ConfigService config)
    {
        ValidateDependencies(env, config);
        _env = env;
        _config = config;
        var directory = Path.Combine(Path.GetFullPath(env.ServerDirectory), "v2");
        var modules = new ServerModules(config, new RepoAccountRoleSummary(directory), new RepoRole(directory), new RepoWorld(directory, Report), _ => new ActorHandlers<LoginContext>()
            .Register<ProtocolV2.Login, ProtocolV2.LoginRt>(new LoginHandler())
            .Register<ProtocolV2.CreateRole, ProtocolV2.CreateRoleRt>(new CreateRoleHandler())
            .Register<ProtocolV2.DeleteRole, ProtocolV2.DeleteRoleRt>(new DeleteRoleHandler()), report: Report);
        _runtime = CreateRuntime(modules);
    }
    /// <summary>验收注入已完整装配的模块，配置直接来自模块，避免重复传入与不一致。</summary>
    internal MockServerService(EnvService env, ServerModules modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ValidateDependencies(env, modules.Config);
        _env = env;
        _config = modules.Config;
        _runtime = CreateRuntime(modules);
    }
    /// <summary>只在服务入口拒绝缺失或失效依赖，后续装配不传递空配置。</summary>
    private static void ValidateDependencies(EnvService env, ConfigService config)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(config);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
    }
    /// <summary>接管完整模块并绑定本服务诊断，不再兜底创建依赖。</summary>
    private ServerRuntime CreateRuntime(ServerModules modules)
    {
        var runtime = new ServerRuntime(modules);
        runtime.Error += Report;
        return runtime;
    }
    /// <summary>由 Net 的传输端建立连接，生成的身份不来自协议载荷。</summary>
    public DataConnectionId OpenConnection() { EnsureActive(); return _runtime.OpenConnection(); }
    /// <summary>接纳指定连接的字节副本并等待该请求处理完成。</summary>
    public Task ReceiveAsync(DataConnectionId id, byte[] packet) { EnsureActive(); return _runtime.ReceiveAsync(id, packet); }
    /// <summary>切换同一连接的代次，保留已经绑定的账号。</summary>
    public ulong ResetSession(DataConnectionId id) { EnsureActive(); return _runtime.ResetSession(id); }
    /// <summary>在 Net 固定帧中读取独立输出字节。</summary>
    public bool TryReadOutgoing(DataConnectionId id, out byte[] packet) { EnsureActive(); return _runtime.TryReadOutgoing(id, out packet); }
    /// <summary>返回独立的连接身份快照。</summary>
    public DataSessionSnapshot Session(DataConnectionId id) { EnsureActive(); return _runtime.Session(id); }
    /// <summary>断开立即使结果失效，随后等待该连接已接纳工作结束。</summary>
    public Task CloseConnectionAsync(DataConnectionId id) { EnsureActive(); return _runtime.CloseConnectionAsync(id); }
    /// <summary>显式保存等待调用前已接纳请求，不受周期禁用影响。</summary>
    public Task FlushAsync() { EnsureActive(); return _runtime.FlushAsync(); }
    /// <summary>由 Net 帧推进计时，文件保存留在后台；已有周期轮次未完则合并本次触发。</summary>
    public void Tick(double delta)
    {
        lock (_disposeGate)
        {
            EnsureActive();
            if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            var interval = _env.ServerFlushIntervalSeconds;
            if (_lastInterval != interval) { _lastInterval = interval; _flushElapsed = 0; }
            if (interval == 0) return;
            _flushElapsed += delta;
            if (_flushElapsed < interval) return;
            _flushElapsed %= interval;
            if (!_periodicFlush.IsCompleted) return;
            _periodicFlush = Task.Run(async () =>
            {
                try { await _runtime.FlushAsync().ConfigureAwait(false); }
                catch (Exception error) { Report(error); }
            });
        }
    }

    /// <summary>客户端同步服务生命周期等待纯后台框架有序停止。</summary>
    public override void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>停止期间拒绝新请求，重复释放等待同一任务。</summary>
    public ValueTask DisposeAsync()
    { lock (_disposeGate) return new(_disposing ??= DisposeCoreAsync()); }
    /// <summary>先排空所有请求及 Actor，再撤销诊断；不释放借用配置。</summary>
    private async Task DisposeCoreAsync()
    {
        IsDisposed = true;
        try
        {
            await _periodicFlush.ConfigureAwait(false);
            await _runtime.StopAsync().ConfigureAwait(false);
        }
        finally { Error = null; }
    }
    /// <summary>服务和借用配置都必须仍然有效。</summary>
    private void EnsureActive()
    { ThrowIfDisposed(); ObjectDisposedException.ThrowIf(_config.IsDisposed, _config); ObjectDisposedException.ThrowIf(_env.IsDisposed, _env); }
    /// <summary>逐一隔离诊断监听，避免影响业务回合或其他连接。</summary>
    private void Report(Exception error)
    {
        if (Error is not { } observers) return;
        foreach (Action<Exception> observer in observers.GetInvocationList())
            try { observer(error); }
            catch (Exception observerError) { System.Diagnostics.Trace.WriteLine(observerError); }
    }
}
