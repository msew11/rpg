using System;
using System.Threading.Tasks;
using Rpg.Config;
using Rpg.Services;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Hosting;

/// <summary>Net 拥有的进程内服务端子服务，持有配置并装配 V2 框架。</summary>
public sealed class MockServerService : Service, IAsyncDisposable
{
    /// <summary>借用先于本服务创建、后于本服务释放的配置。</summary>
    private readonly ConfigService _config;
    /// <summary>本服务拥有的纯 .NET 运行实例。</summary>
    private readonly ServerRuntime _runtime;
    /// <summary>并发释放共享的停止任务。</summary>
    private readonly object _disposeGate = new();
    /// <summary>非空后不再接纳外部工作。</summary>
    private Task? _disposing;
    /// <summary>后续业务装配可使用同一个配置实例。</summary>
    public ConfigService Config { get { EnsureActive(); return _config; } }
    /// <summary>意外处理失败与 Actor 通知错误的诊断，不包含完整请求载荷。</summary>
    public event Action<Exception>? Error;

    /// <summary>公开构造只声明服务依赖；默认仅装配框架处理器。</summary>
    public MockServerService(ConfigService config) : this(config, null) { }
    /// <summary>验收可注入纯内存模块，不替换正式配置实例。</summary>
    internal MockServerService(ConfigService config, ServerModules? modules)
    {
        ArgumentNullException.ThrowIfNull(config);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        _config = config;
        modules ??= new ServerModules(_ => new ActorHandlers<LoginContext>()
            .Register<DataLogin, DataLoginResult>(new LoginHandler(config)), report: Report);
        _runtime = new(modules);
        _runtime.Error += Report;
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

    /// <summary>客户端同步服务生命周期等待纯后台框架有序停止。</summary>
    public override void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>停止期间拒绝新请求，重复释放等待同一任务。</summary>
    public ValueTask DisposeAsync()
    { lock (_disposeGate) return new(_disposing ??= DisposeCoreAsync()); }
    /// <summary>先排空所有请求及 Actor，再撤销诊断；不释放借用配置。</summary>
    private async Task DisposeCoreAsync()
    {
        IsDisposed = true;
        try { await _runtime.StopAsync().ConfigureAwait(false); }
        finally { Error = null; }
    }
    /// <summary>服务和借用配置都必须仍然有效。</summary>
    private void EnsureActive()
    { ThrowIfDisposed(); ObjectDisposedException.ThrowIf(_config.IsDisposed, _config); }
    /// <summary>逐一隔离诊断监听，避免影响业务回合或其他连接。</summary>
    private void Report(Exception error)
    {
        if (Error is not { } observers) return;
        foreach (Action<Exception> observer in observers.GetInvocationList())
            try { observer(error); }
            catch (Exception observerError) { System.Diagnostics.Trace.WriteLine(observerError); }
    }
}
