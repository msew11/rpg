using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Rpg.ProtocolV2;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Hosting;

/// <summary>控制权、观察者投递和独立时钟的运行时协调，不将连接元数据写入业务协议。</summary>
internal sealed class WorldCoordinator
{
    /// <summary>所属服务器模块。</summary>
    private readonly ServerModules _modules;
    /// <summary>只串行控制变更，不围住普通移动。</summary>
    private readonly SemaphoreSlim _control = new(1, 1);
    /// <summary>权威轮次与保存屏障。</summary>
    private readonly SemaphoreSlim _step = new(1, 1);
    /// <summary>角色当前唯一控制者。</summary>
    private readonly ConcurrentDictionary<long, DataControlBinding> _bindings = new();
    /// <summary>移动或投影重试角色，值为调度工作版本。</summary>
    private readonly ConcurrentDictionary<long, long> _moving = new();
    /// <summary>服务器单调时钟。</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    /// <summary>控制和调度的单调计数器。</summary>
    private long _version;
    /// <summary>下次发布截止时间。</summary>
    private long _publishAt;
    /// <summary>当前服务器生效速度。</summary>
    private int _speed = 180;
    /// <summary>速度改变时为静止角色也安排一次完整位置投影。</summary>
    internal int Speed
    {
        get => Volatile.Read(ref _speed);
        set { if (Interlocked.Exchange(ref _speed, value) == value) return; foreach (var id in _bindings.Keys) _moving[id] = Interlocked.Increment(ref _version); }
    }
    /// <summary>当前服务器时间，零保留给未初始化状态。</summary>
    internal long Now => Math.Max(1, _clock.ElapsedMilliseconds);
    /// <summary>逻辑轮次耗时样本，供独立负载验收读取。</summary>
    internal ConcurrentQueue<double> RoundMilliseconds { get; } = new();
    /// <summary>不影响其他轮次的诊断入口。</summary>
    private readonly Action<Exception> _report;
    /// <summary>建立本实例独立协调器。</summary>
    internal WorldCoordinator(ServerModules modules, Action<Exception> report) { _modules = modules; _report = report; }
    /// <summary>构造服务器内部可信调用身份。</summary>
    internal DataActorCall Call(DataSessionSnapshot session, bool role = false)
    {
        var binding = role ? Owned(session) : null;
        return new(session.ConnectionId.Value, session.Generation, session.AuthEpoch, binding?.RoleId ?? 0, binding?.Version ?? 0, Now, Speed);
    }
    /// <summary>当前连接必须仍控制会话中的角色。</summary>
    private DataControlBinding Owned(DataSessionSnapshot session) => session.Username is not null && session.RoleId > 0 &&
        _bindings.TryGetValue(session.RoleId, out var binding) && binding.Connection.Id == session.ConnectionId
        ? binding : throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "连接没有当前角色控制权");
    /// <summary>请求完成后登记最新移动工作，防止并发轮次清除新意图。</summary>
    internal void Moved(DataSessionSnapshot session) { var binding = Owned(session); _moving[binding.RoleId] = Interlocked.Increment(ref _version); }
    /// <summary>进入角色时建立控制屏障和初始观察状态，原始私有数据沿用已验证回复。</summary>
    internal async Task<(DataControlBinding Binding, WorldFramePb Initial)?> EnterAsync(ConnectionSession connection, DataSessionSnapshot expected, EnterWorldRt entry)
    {
        await _control.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!connection.IsCurrent(expected)) return null;
            var oldBindings = _bindings.Values.Where(item => item.Connection == connection && item.RoleId != entry.State.RoleId).ToArray();
            if (_bindings.TryGetValue(entry.State.RoleId, out var previous)) await RemoveAsync(previous, previous.Connection != connection).ConfigureAwait(false);
            if (!connection.IsCurrent(expected)) return null;
            var binding = new DataControlBinding(connection, entry.State.RoleId, entry.State.Position.MapId, Interlocked.Increment(ref _version));
            _modules.Calls.SetControl(binding.RoleId, binding.Version); _bindings[binding.RoleId] = binding;
            try
            {
                await _modules.Role(binding.RoleId).Ask<bool>(new DataRoleControl(Now, Speed)).ConfigureAwait(false);
                await _modules.World(binding.MapId).Ask<bool>(new DataWorldMembership(binding.RoleId, true)).ConfigureAwait(false);
                await _modules.Role(binding.RoleId).Ask<AdvanceRoleRt>(new AdvanceRole { ServerTimeMs = Now, MovementSpeed = Speed }, binding.Scope(Now, Speed)).ConfigureAwait(false);
                var initial = await _modules.World(binding.MapId).Ask<GetWorldSnapshotRt>(new GetWorldSnapshot { RoleId = binding.RoleId, ServerTimeMs = Now }, binding.Scope(Now, Speed)).ConfigureAwait(false);
                if (!connection.IsCurrent(expected)) { await RemoveAsync(binding, false).ConfigureAwait(false); return null; }
                foreach (var old in oldBindings) await RemoveAsync(old, false).ConfigureAwait(false);
                entry.State.Position = initial.Frame.Self.Position.Clone(); entry.Map = initial.Frame.Snapshot.Map.Clone();
                return (binding, initial.Frame);
            }
            catch { await RemoveAsync(binding, false).ConfigureAwait(false); throw; }
        }
        finally { _control.Release(); }
    }
    /// <summary>断线或重新认证时仅解绑该连接仍拥有的角色。</summary>
    internal async Task DetachAsync(ConnectionSession connection)
    {
        await _control.WaitAsync().ConfigureAwait(false);
        try { foreach (var binding in _bindings.Values.Where(item => item.Connection == connection).ToArray()) await RemoveAsync(binding, false).ConfigureAwait(false); }
        finally { _control.Release(); }
    }
    /// <summary>删除在线角色时撤销控制和认证，不定位闲置角色。</summary>
    internal async Task DeletedAsync(long roleId, ConnectionSession requester)
    {
        await _control.WaitAsync().ConfigureAwait(false);
        try { if (_bindings.TryGetValue(roleId, out var binding)) await RemoveAsync(binding, binding.Connection != requester, "当前角色已删除，请重新登录").ConfigureAwait(false); }
        finally { _control.Release(); }
    }
    /// <summary>控制目录先失效，再在角色和地图邮箱中排空旧状态。</summary>
    private async Task RemoveAsync(DataControlBinding binding, bool kick, string message = "角色已在另一连接进入，请重新登录")
    {
        if (!_bindings.TryGetValue(binding.RoleId, out var current) || current != binding) return;
        _bindings.TryRemove(binding.RoleId, out _); _moving.TryRemove(binding.RoleId, out _); _modules.Calls.RemoveControl(binding.RoleId);
        await _modules.Role(binding.RoleId).Ask<bool>(new DataRoleControl(Now, Speed)).ConfigureAwait(false);
        await _modules.World(binding.MapId).Ask<bool>(new DataWorldMembership(binding.RoleId, false)).ConfigureAwait(false);
        binding.Connection.UnbindRole(binding.RoleId);
        if (kick)
        {
            binding.Connection.Revoke(message);
            var snapshot = binding.Connection.Snapshot(); _modules.Calls.SetConnection(snapshot.ConnectionId.Value, snapshot.Generation, snapshot.AuthEpoch);
        }
    }
    /// <summary>首帧必须排在进入响应之后，普通发布可先等待首帧到达。</summary>
    internal void Initial(DataControlBinding binding, DataSessionSnapshot expected, WorldFramePb frame) => Publish(binding, expected, frame, new WorldUpdateNotice { Frame = frame }, 0);
    /// <summary>重同步只构建业务快照，原连接元数据留在运行时。</summary>
    internal async Task ResyncAsync(ConnectionSession connection, DataSessionSnapshot expected, ulong requestId)
    {
        var binding = Owned(expected);
        var reply = await _modules.World(binding.MapId).Ask<GetWorldSnapshotRt>(new GetWorldSnapshot { RoleId = binding.RoleId, ServerTimeMs = Now }, binding.Scope(Now, Speed)).ConfigureAwait(false);
        Publish(binding, expected, reply.Frame, new ResyncWorldRt { Frame = reply.Frame }, requestId);
    }
    /// <summary>按世界帧序号发布，旧代次占位也按序消耗，避免阻塞新代次。</summary>
    private void Publish(DataControlBinding binding, DataSessionSnapshot expected, WorldFramePb frame, IMessage message, ulong requestId)
    {
        if (!_bindings.TryGetValue(binding.RoleId, out var active) || active != binding) return;
        lock (binding.Output)
        {
            if (frame.Sequence < binding.NextSequence) return;
            binding.Output[frame.Sequence] = new(expected, Protocol.MessageCodec.Encode(message, expected.Generation, requestId, false));
            while (binding.Output.Remove(binding.NextSequence, out var delivery))
            {
                binding.NextSequence++;
                if (binding.Connection.IsCurrent(delivery.Expected, true)) binding.Connection.Complete(delivery.Expected, delivery.Packet);
            }
        }
    }
    /// <summary>独立单轮推进并合并发布截止时间，不累积过期轮次。</summary>
    internal async Task AdvanceAsync()
    {
        await _step.WaitAsync().ConfigureAwait(false); var started = Stopwatch.GetTimestamp();
        try
        {
            var now = Now;
            await Task.WhenAll(_moving.ToArray().Select(async item =>
            {
                try
                {
                    if (!_bindings.TryGetValue(item.Key, out var binding)) return;
                    var reply = await _modules.Role(item.Key).Ask<AdvanceRoleRt>(new AdvanceRole { ServerTimeMs = now, MovementSpeed = Speed }, binding.Scope(now, Speed)).ConfigureAwait(false);
                    if (!reply.Moving) ((ICollection<KeyValuePair<long, long>>)_moving).Remove(item);
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { _report(error); }
            })).ConfigureAwait(false);
            if (now < _publishAt) return; _publishAt = now + 100;
            var routes = new List<(DataControlBinding Binding, DataSessionSnapshot Expected)>();
            foreach (var binding in _bindings.Values)
            {
                try { var expected = binding.Connection.Snapshot(); if (expected.RoleId == binding.RoleId && expected.Username is not null) routes.Add((binding, expected)); }
                catch (ObjectDisposedException) { }
            }
            await Task.WhenAll(routes.GroupBy(item => item.Binding.MapId).Select(async group =>
            {
                var captured = group.ToDictionary(item => item.Binding.RoleId); var request = new BuildWorldUpdates { ServerTimeMs = now };
                request.ObserverRoleIds.AddRange(captured.Keys);
                BuildWorldUpdatesRt reply;
                try { reply = await _modules.World(group.Key).Ask<BuildWorldUpdatesRt>(request, new DataActorCall(Observers: captured.ToImmutableDictionary(pair => pair.Key,
                    pair => new DataActorCall(pair.Value.Expected.ConnectionId.Value, pair.Value.Expected.Generation, pair.Value.Expected.AuthEpoch, pair.Key, pair.Value.Binding.Version)))).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                foreach (var delivery in reply.Deliveries)
                    if (captured.TryGetValue(delivery.RoleId, out var route)) Publish(route.Binding, route.Expected, delivery.Notice.Frame, delivery.Notice, 0);
            })).ConfigureAwait(false);
        }
        finally { RoundMilliseconds.Enqueue(Stopwatch.GetElapsedTime(started).TotalMilliseconds); if (RoundMilliseconds.Count > 10000) RoundMilliseconds.TryDequeue(out _); _step.Release(); }
    }
    /// <summary>等待已经接纳的权威轮次，未来轮次不属于本次保存屏障。</summary>
    internal async Task BarrierAsync() { await _step.WaitAsync().ConfigureAwait(false); _step.Release(); }
    /// <summary>服务器停止前物化所有已接受目标并解除在线状态。</summary>
    internal async Task StopAsync()
    {
        await BarrierAsync().ConfigureAwait(false); await _control.WaitAsync().ConfigureAwait(false);
        try { foreach (var binding in _bindings.Values.ToArray()) await RemoveAsync(binding, false).ConfigureAwait(false); }
        finally { _control.Release(); }
    }
    /// <summary>服务器内部控制与发布状态，不作为 Actor 消息载荷。</summary>
    internal sealed class DataControlBinding(ConnectionSession connection, long roleId, int mapId, long version)
    {
        /// <summary>实际投递连接。</summary>
        internal ConnectionSession Connection { get; } = connection;
        /// <summary>受控角色。</summary>
        internal long RoleId { get; } = roleId;
        /// <summary>角色所属地图。</summary>
        internal int MapId { get; } = mapId;
        /// <summary>服务器独有控制版本。</summary>
        internal long Version { get; } = version;
        /// <summary>等待有序发布的完成结果。</summary>
        internal SortedDictionary<ulong, DataFrameDelivery> Output { get; } = new();
        /// <summary>下一待发布序号。</summary>
        internal ulong NextSequence { get; set; } = 1;
        /// <summary>不绑定客户端代次的系统控制调用。</summary>
        internal DataActorCall Scope(long now, int speed) => new(RoleId: RoleId, ControlVersion: Version, ServerTimeMs: now, MovementSpeed: speed);
    }
    /// <summary>原任务捕获的投递身份与独立字节。</summary>
    internal sealed record DataFrameDelivery(DataSessionSnapshot Expected, byte[] Packet);
}
