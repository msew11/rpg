using System;
using System.Collections.Concurrent;

namespace Rpg.Server.Runtime;

/// <summary>邮箱侧不可变投递身份；不序列化，不包装业务协议。</summary>
public sealed record DataActorCall(Guid ConnectionId = default, ulong Generation = 0, long AuthEpoch = 0,
    long RoleId = 0, long ControlVersion = 0, long ServerTimeMs = 0, int MovementSpeed = 0, System.Collections.Immutable.ImmutableDictionary<long, DataActorCall>? Observers = null, bool PreserveResult = false);

/// <summary>每个服务器独有的投递资格目录，不保存角色业务状态。</summary>
internal sealed class ActorCallRegistry
{
    /// <summary>当前连接的代次与认证版本。</summary>
    private readonly ConcurrentDictionary<Guid, (ulong Generation, long AuthEpoch)> _connections = new();
    /// <summary>当前角色控制版本。</summary>
    private readonly ConcurrentDictionary<long, long> _controls = new();
    /// <summary>发布当前连接身份。</summary>
    internal void SetConnection(Guid id, ulong generation, long authEpoch) => _connections[id] = (generation, authEpoch);
    /// <summary>撤销已经断开的连接。</summary>
    internal void RemoveConnection(Guid id) => _connections.TryRemove(id, out _);
    /// <summary>切换当前角色控制版本。</summary>
    internal void SetControl(long roleId, long version) => _controls[roleId] = version;
    /// <summary>撤销角色控制。</summary>
    internal void RemoveControl(long roleId) => _controls.TryRemove(roleId, out _);
    /// <summary>纯系统调用无连接；控制版本和连接版本按需独立检查。</summary>
    internal bool IsCurrent(DataActorCall call) =>
        (call.ConnectionId == Guid.Empty || _connections.TryGetValue(call.ConnectionId, out var connection) && connection == (call.Generation, call.AuthEpoch)) &&
        (call.ControlVersion == 0 || _controls.TryGetValue(call.RoleId, out var version) && version == call.ControlVersion) &&
        (call.Observers is null || System.Linq.Enumerable.All(call.Observers.Values, IsCurrent));
}
