using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using Rpg.Server.Connections.Data;

namespace Rpg.Server.Connections;

/// <summary>连接独占的接纳队列、代次和账号绑定，不持有角色或地图业务状态。</summary>
internal sealed class ConnectionSession
{
    /// <summary>全局输出预算。</summary>
    private readonly OutputBudget _budget;
    /// <summary>当前连接排队字节。</summary>
    private int _outputBytes;
    /// <summary>输出可读或连接结束信号。</summary>
    private readonly System.Threading.SemaphoreSlim _outputReady = new(0);
    /// <summary>等待输出，网络循环无需轮询。</summary>
    internal Task WaitOutgoingAsync(System.Threading.CancellationToken cancellation) => _outputReady.WaitAsync(cancellation);
    /// <summary>保护会话身份、接纳和输出的短锁。</summary>
    private readonly object _gate = new();
    /// <summary>逐个处理本连接请求，其他连接不共用此消费任务。</summary>
    private readonly Channel<Delivery> _requests = Channel.CreateUnbounded<Delivery>(new() { SingleReader = true });
    /// <summary>等待客户端固定帧读取的独立输出字节。</summary>
    private readonly Queue<byte[]> _outgoing = new();
    /// <summary>宿主装配的请求入口，不作为 Actor 消息传递。</summary>
    private readonly Func<ConnectionSession, byte[], Task> _process;
    /// <summary>按需启动的串行请求任务。</summary>
    private Task? _loop;
    /// <summary>连接是否仍有效，断开后结果不可再绑定或输出。</summary>
    private bool _active = true;
    /// <summary>是否还接纳外部请求。</summary>
    private bool _accepting = true;
    /// <summary>同连接单调递增的代次。</summary>
    private ulong _generation = 1;
    /// <summary>本连接认证后绑定的账号；与其他连接分别保存。</summary>
    private string? _username;
    /// <summary>每次认证或撤销递增，独立于客户端代次。</summary>
    private long _authEpoch;
    /// <summary>当前连接进入成功的角色元数据，不表示在线成员或独占。</summary>
    private long _roleId;
    /// <summary>服务端生成的不可变连接身份。</summary>
    internal DataConnectionId Id { get; }

    /// <summary>建立连接会话，尚无账号身份。</summary>
    internal ConnectionSession(DataConnectionId id, Func<ConnectionSession, byte[], Task> process, OutputBudget? budget = null)
    { Id = id; _process = process; _budget = budget ?? new OutputBudget(); }

    /// <summary>取得独立只读身份，已断开的连接不可读取。</summary>
    internal DataSessionSnapshot Snapshot()
    { lock (_gate) { EnsureActive(); return new(Id, _generation, _username, _roleId, _authEpoch); } }

    /// <summary>复制后入队，返回任务表示本请求已经处理完成或明确失败。</summary>
    internal Task ReceiveAsync(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        lock (_gate)
        {
            EnsureActive();
            ObjectDisposedException.ThrowIf(!_accepting, this);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_requests.Writer.TryWrite(new((byte[])packet.Clone(), completion))) throw new ObjectDisposedException(nameof(ConnectionSession));
            _loop ??= Task.Run(RunAsync);
            return completion.Task;
        }
    }
    /// <summary>捕获当前队列尾部屏障，等待先前请求但不关闭连接、不切换代次。</summary>
    internal Task BarrierAsync()
    {
        lock (_gate)
        {
            if (!_accepting || _loop is null) return _loop ?? Task.CompletedTask;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_requests.Writer.TryWrite(new(null, completion))) throw new ObjectDisposedException(nameof(ConnectionSession));
            return completion.Task;
        }
    }

    /// <summary>检查请求仍属当前代次，并取得稳定的身份快照。</summary>
    internal DataSessionSnapshot? Begin(ulong generation, bool login)
    {
        lock (_gate)
        {
            if (!_active || generation < _generation) return null;
            if (generation > _generation) { _generation = generation; ClearOutput(); }
            if (login) { _authEpoch++; _username = null; _roleId = 0; }
            return new(Id, _generation, _username, _roleId, _authEpoch);
        }
    }

    /// <summary>在同一短锁内验证代次、绑定成功账号并排入输出。</summary>
    internal void Complete(ulong generation, byte[] packet, string? username = null, long? roleId = null)
    {
        lock (_gate)
        {
            if (!_active || generation != _generation) return;
            if (username is not null) _username = username;
            if (roleId.HasValue) _roleId = roleId.Value;
            if (_outgoing.Count >= 64 || _outputBytes + packet.Length > 8 * 1024 * 1024 || !_budget.Reserve(packet.Length))
            { _active = false; _username = null; _roleId = 0; ClearOutput(); _outputReady.Release(); return; }
            _outgoing.Enqueue(packet); _outputBytes += packet.Length; _outputReady.Release();
        }
    }

    /// <summary>统一检查原请求的代次和认证身份，可选角色绑定。</summary>
    internal bool IsCurrent(DataSessionSnapshot expected, bool role = false)
    { lock (_gate) return _active && _generation == expected.Generation && _authEpoch == expected.AuthEpoch && (!role || _roleId == expected.RoleId); }
    /// <summary>同一短锁内核对认证版本、绑定成功结果并发布。</summary>
    internal bool Complete(DataSessionSnapshot expected, byte[] packet, string? username = null, long? roleId = null)
    {
        lock (_gate)
        {
            if (!IsCurrent(expected)) return false;
            Complete(expected.Generation, packet, username, roleId); return _active;
        }
    }
    /// <summary>只撤销仍匹配的角色绑定，账号管理请求仍能完成。</summary>
    internal void UnbindRole(long roleId) { lock (_gate) if (_roleId == roleId) _roleId = 0; }
    /// <summary>运行时撤销认证并按当前代次发布原因，保留物理连接。</summary>
    internal void Revoke(string message)
    {
        lock (_gate)
        {
            if (!_active) return;
            _authEpoch++; _username = null; _roleId = 0; ClearOutput();
            Complete(_generation, global::Rpg.Server.Protocol.MessageCodec.Encode(new global::Rpg.Protocol.SessionKickedNotice { Message = message }, _generation, 0, false));
        }
    }
    /// <summary>重置代次、清空旧输出，但保留已认证账号。</summary>
    internal ulong Reset()
    {
        lock (_gate)
        {
            EnsureActive();
            _generation = checked(_generation + 1);
            ClearOutput();
            return _generation;
        }
    }

    /// <summary>只在调用方主动读取时移交输出字节，不调用后台客户端监听。</summary>
    internal bool TryRead(out byte[] packet)
    {
        lock (_gate)
        {
            EnsureActive();
            if (!_outgoing.TryDequeue(out packet!)) return false;
            _outputBytes -= packet.Length; _budget.Release(packet.Length); return true;
        }
    }

    /// <summary>停止接纳并排空；断开还立即清除身份与输出。</summary>
    internal Task DrainAsync(bool disconnect = false)
    {
        lock (_gate)
        {
            _accepting = false;
            if (disconnect) { _active = false; _username = null; _roleId = 0; ClearOutput(); _outputReady.Release(); }
            _requests.Writer.TryComplete();
            return _loop ?? Task.CompletedTask;
        }
    }

    /// <summary>逐个等待请求，单条失败交还调用方且不终止本连接。</summary>
    private async Task RunAsync()
    {
        await foreach (var delivery in _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (delivery.Packet is not null) await _process(this, delivery.Packet).ConfigureAwait(false);
                delivery.Completion.TrySetResult();
            }
            catch (Exception error) { delivery.Completion.TrySetException(error); }
        }
    }

    /// <summary>归还所有待发预算，调用方持有会话锁。</summary>
    private void ClearOutput() { while (_outgoing.TryDequeue(out var packet)) _budget.Release(packet.Length); _outputBytes = 0; }
    /// <summary>拒绝已经断开的连接继续操作。</summary>
    private void EnsureActive() => ObjectDisposedException.ThrowIf(!_active, this);
    /// <summary>一条已复制的请求与其处理完成句柄。</summary>
    private sealed record Delivery(byte[]? Packet, TaskCompletionSource Completion);
}
