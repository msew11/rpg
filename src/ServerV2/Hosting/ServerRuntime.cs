using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Codec = Rpg.ServerV2.Protocol.MessageCodec;
using Google.Protobuf;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Routing;

namespace Rpg.ServerV2.Hosting;

/// <summary>纯 .NET 宿主，装配连接、路由和模块，不执行具体业务或磁盘操作。</summary>
internal sealed class ServerRuntime : IAsyncDisposable
{
    /// <summary>当前实例输出预算。</summary>
    private readonly OutputBudget _outputBudget = new();
    /// <summary>异步等待指定连接输出。</summary>
    internal Task WaitOutgoingAsync(DataConnectionId id, System.Threading.CancellationToken cancellation)
    { lock (_gate) return Find(id).WaitOutgoingAsync(cancellation); }
    /// <summary>只保护外部接纳与连接目录。</summary>
    private readonly object _gate = new();
    /// <summary>当前连接及仍在排空的已断开连接。</summary>
    private readonly Dictionary<DataConnectionId, ConnectionSession> _connections = new();
    /// <summary>宿主拥有的三类模块目录。</summary>
    private readonly ServerModules _modules;
    /// <summary>明确的协议到模块路由。</summary>
    internal RequestRouter Router { get; }
    /// <summary>停止任务一旦建立即不再接纳外部工作。</summary>
    private Task? _stopping;
    /// <summary>所有保存轮次串行衔接，关闭等待最后一个已接纳保存。</summary>
    private Task _flushTail = Task.CompletedTask;
    /// <summary>不影响请求结果的服务端诊断。</summary>
    internal event Action<Exception>? Error;

    /// <summary>接管已显式组装的模块目录。</summary>
    internal ServerRuntime(ServerModules modules) { _modules = modules; Router = new(modules); }
    /// <summary>分配新连接身份，初始代次为 1 且尚未绑定账号。</summary>
    internal DataConnectionId OpenConnection()
    {
        lock (_gate)
        {
            EnsureAccepting();
            var id = new DataConnectionId(Guid.NewGuid());
            _connections.Add(id, new(id, ProcessAsync, _outputBudget));
            return id;
        }
    }
    /// <summary>按传输绑定的连接接纳字节，不能从载荷指定其他连接。</summary>
    internal Task ReceiveAsync(DataConnectionId id, byte[] packet)
    { lock (_gate) { EnsureAccepting(); return Find(id).ReceiveAsync(packet); } }
    /// <summary>会话重置不销毁共享 Actor，也不清除账号绑定。</summary>
    internal ulong ResetSession(DataConnectionId id)
    { lock (_gate) { EnsureAccepting(); return Find(id).Reset(); } }
    /// <summary>输出由调用方拉取，停止排空期间仍允许读取已完成响应。</summary>
    internal bool TryReadOutgoing(DataConnectionId id, out byte[] packet)
    { lock (_gate) return Find(id).TryRead(out packet); }
    /// <summary>获取服务端持有的不可变连接身份快照。</summary>
    internal DataSessionSnapshot Session(DataConnectionId id)
    { lock (_gate) return Find(id).Snapshot(); }

    /// <summary>立即使连接失效，再等待已接纳请求结束后移除目录项。</summary>
    internal async Task CloseConnectionAsync(DataConnectionId id)
    {
        ConnectionSession session;
        Task drained;
        lock (_gate) { session = Find(id); drained = session.DrainAsync(true); }
        await drained.ConfigureAwait(false);
        lock (_gate) _connections.Remove(id);
    }

    /// <summary>先冻结外部接纳并排空请求，再终止所有模块。</summary>
    internal Task StopAsync()
    {
        lock (_gate)
            return _stopping ??= StopCoreAsync(_connections.Values.Select(session => session.DrainAsync()).ToArray(), _flushTail);
    }
    /// <summary>同步捕获已接纳请求屏障，每次显式调用都执行独立的后续保存轮次。</summary>
    internal Task FlushAsync()
    {
        lock (_gate)
        {
            EnsureAccepting();
            var barriers = _connections.Values.Select(session => session.BarrierAsync()).ToArray();
            return _flushTail = FlushCoreAsync(_flushTail, barriers);
        }
    }
    /// <summary>上一轮失败不阻止重试；屏障后才枚举已经启动的 Actor。</summary>
    private async Task FlushCoreAsync(Task previous, Task[] barriers)
    {
        try { await previous.ConfigureAwait(false); } catch { /* 上轮错误由该轮调用方观察。 */ }
        await Task.WhenAll(barriers).ConfigureAwait(false);
        await _modules.FlushAsync().ConfigureAwait(false);
    }
    /// <summary>等待有序停止。</summary>
    public ValueTask DisposeAsync() => new(StopAsync());

    /// <summary>只在每连接消费任务中调用；异步完成时不使用最新代次冒充原请求。</summary>
    private async Task ProcessAsync(ConnectionSession session, byte[] packet)
    {
        var envelope = Codec.ReadEnvelope(packet);
        if (session.Begin(envelope.Generation, false) is not { } snapshot) return;
        IMessage response;
        string? bindUsername = null;
        long? bindRoleId = null;
        try
        {
            var request = Codec.ReadPayload(envelope, true);
            if (session.Begin(envelope.Generation, request is ProtocolV2.Login) is not { } current) return;
            var result = await Router.RouteAsync(current, request).ConfigureAwait(false);
            response = result.Message;
            bindUsername = result.BindUsername;
            bindRoleId = result.BindRoleId;
        }
        catch (ServerRequestException error) { response = new ErrorRt { Code = error.Code, Message = error.Message }; }
        catch (Exception error)
        {
            Report(error);
            response = new ErrorRt { Code = ErrorCodePb.InternalError, Message = "服务端处理失败" };
        }
        if (envelope.RequestId == 0) throw new ServerRequestException(ErrorCodePb.InvalidRequest, "请求编号必须非零，无法关联响应");
        session.Complete(snapshot.Generation, Codec.Encode(response, snapshot.Generation, envelope.RequestId, false), bindUsername, bindRoleId);
    }

    /// <summary>已接纳回合仍可以访问下游，完成后才关闭模块目录。</summary>
    private async Task StopCoreAsync(Task[] pending, Task flushing)
    {
        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
            try { await flushing.ConfigureAwait(false); } catch { /* 最终保存会重试，先前错误由原调用方观察。 */ }
            await _modules.FlushAsync().ConfigureAwait(false);
        }
        finally { await _modules.DisposeAsync().ConfigureAwait(false); }
    }
    /// <summary>查找已有连接，不自动创建或恢复已断开会话。</summary>
    private ConnectionSession Find(DataConnectionId id) => _connections.TryGetValue(id, out var session)
        ? session : throw new InvalidOperationException("连接不存在或已结束");
    /// <summary>拒绝停止期间新增外部工作。</summary>
    private void EnsureAccepting() => ObjectDisposedException.ThrowIf(_stopping is not null, this);
    /// <summary>隔离诊断观察者，避免影响其他连接。</summary>
    private void Report(Exception error)
    {
        if (Error is not { } observers) return;
        foreach (Action<Exception> observer in observers.GetInvocationList())
            try { observer(error); }
            catch (Exception observerError) { System.Diagnostics.Trace.WriteLine(observerError); }
    }
}
