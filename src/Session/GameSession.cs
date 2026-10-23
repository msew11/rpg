using System;
using Godot;
using Rpg.Net;
using Rpg.Net.Message;

namespace Rpg.Session;

/// <summary>绑定一个角色、功能缓存和网络会话代次。</summary>
public sealed class GameSession : IDisposable
{
    /// <summary>本会话借用的常驻网络管理器。</summary>
    private readonly NetManager _net;
    /// <summary>创建时独占的网络会话代次。</summary>
    private readonly long _generation;
    /// <summary>是否已结束此角色会话。</summary>
    private bool _disposed;

    /// <summary>请求选择的角色 ID。</summary>
    public long RoleId { get; }
    /// <summary>成功选角后可读取的角色快照。</summary>
    public DataRole Role => Cache.Role.State.Role;
    /// <summary>本会话组合的服务器数据缓存。</summary>
    public DataCache Cache { get; }
    /// <summary>供当前角色 UI 注册响应和推送的事件系统。</summary>
    public EventSystem Events => _net.Events;

    /// <summary>先隔离旧代次并建立缓存监听，由调用方随后发送选角请求。</summary>
    public GameSession(long roleId, NetManager net)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        _net = net;
        _net.ResetSession();
        _generation = net.Generation;
        RoleId = roleId;
        Cache = new DataCache(roleId, net.Events);
    }

    /// <summary>将本地移动意图转换为请求协议。</summary>
    public void SubmitMove(long actionId, Vector2 target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_net.Generation != _generation) throw new InvalidOperationException("角色会话已被替换");
        _net.Send(new Move { RoleId = RoleId, ActionId = actionId, X = target.X, Y = target.Y });
    }

    /// <summary>释放缓存并丢弃本会话消息，不重置后来创建的新会话。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cache.Dispose();
        if (!_net.IsDisposed && _net.Generation == _generation) _net.ResetSession();
    }
}
