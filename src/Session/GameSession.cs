using System;
using Godot;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Cache;
using Rpg.Session.Data;

namespace Rpg.Session;

/// <summary>登录成功后的账号会话，统一持有登录摘要与当前角色缓存。</summary>
public sealed class GameSession : IDisposable
{
    /// <summary>本会话借用的常驻网络服务。</summary>
    private readonly NetService _net;
    /// <summary>当前账号会话拥有的网络代次，选角与离开角色时递增。</summary>
    private long _generation;
    /// <summary>是否已结束此账号会话。</summary>
    private bool _disposed;

    /// <summary>当前选择或等待进入的角色 ID，尚未选角时为零。</summary>
    public long RoleId => RoleCache.RoleId;
    /// <summary>成功选角后可读取的角色快照。</summary>
    public RoleWorldStatePbData Role => RoleCache.State;
    /// <summary>本次登录的账号摘要缓存，切换角色时保留。</summary>
    public LoginCache LoginCache { get; }
    /// <summary>当前角色的世界及后续角色业务数据缓存。</summary>
    public RoleCache RoleCache { get; }
    /// <summary>供会话 UI 注册响应和推送的事件系统。</summary>
    public EventSystem Events => _net.Events;

    /// <summary>从全局入口绑定当前网络，后续服务重建不会改变本会话归属。</summary>
    public GameSession(LoginRt response) : this(GameServices.Net, response) { }

    /// <summary>借用显式网络创建账号会话，供独立实例与隔离测试使用。</summary>
    public GameSession(NetService net, LoginRt response)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Success) throw new InvalidOperationException("登录成功后才能创建游戏会话");
        _net = net;
        LoginCache = new LoginCache(net.Events, response);
        try
        {
            _net.ResetSession();
            _generation = net.Generation;
            RoleCache = new RoleCache(net.Events);
        }
        catch
        {
            LoginCache.Dispose();
            throw;
        }
    }

    /// <summary>保留登录摘要，隔离旧角色消息并清空角色数据后提交选角。</summary>
    public void SelectRole(long roleId)
    {
        EnsureActive();
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        _net.ResetSession();
        _generation = _net.Generation;
        RoleCache.Reset(roleId);
        _net.Send(new SelectRole { RoleId = roleId });
    }

    /// <summary>退出当前角色并取消在途消息，账号会话与登录缓存继续保留。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        if (RoleId == 0) return;
        _net.ResetSession();
        _generation = _net.Generation;
        RoleCache.Reset();
    }

    /// <summary>将本地移动意图转换为请求协议。</summary>
    public void SubmitMove(long actionId, Vector2 target)
    {
        EnsureActive();
        if (!RoleCache.IsInitialized) throw new InvalidOperationException("选角成功后才能提交移动");
        _net.Send(new Move { RoleId = RoleId, ActionId = actionId, X = target.X, Y = target.Y });
    }

    /// <summary>退出账号时释放所有缓存并隔离旧消息，不重置后来创建的新账号会话。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RoleCache.Dispose();
        LoginCache.Dispose();
        if (!_net.IsDisposed && _net.Generation == _generation) _net.ResetSession();
    }

    /// <summary>防止已释放或被替换的账号会话继续操作网络。</summary>
    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_net.IsDisposed, _net);
        if (_net.Generation != _generation) throw new InvalidOperationException("游戏会话已被替换");
    }
}
