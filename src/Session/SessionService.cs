using System;
using System.Collections.Generic;
using Godot;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Data;

namespace Rpg.Session;

/// <summary>拥有账号会话，向节点提供登录、选角和角色快照。</summary>
public sealed class SessionService : IService
{
    /// <summary>绑定本次启动的网络，不随全局入口重建而切换。</summary>
    private readonly NetService _net;
    /// <summary>缓存阶段处理账号建立，失败时阻止普通界面监听。</summary>
    private readonly IDisposable _loginSubscription;
    /// <summary>成功登录后持有的账号会话。</summary>
    private GameSession? _current;
    /// <summary>是否已结束本次服务。</summary>
    private bool _disposed;

    /// <summary>账号是否已认证。</summary>
    public bool IsLoggedIn => _current?.LoginCache.IsLoggedIn == true;
    /// <summary>当前选择或等待进入的角色标识。</summary>
    public long RoleId => _current?.RoleId ?? 0;
    /// <summary>当前角色缓存是否就绪。</summary>
    public bool HasRole => _current?.RoleCache.IsInitialized == true;
    /// <summary>已转换的只读登录摘要，未登录时为空。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles => _current?.LoginCache.Roles ?? Array.Empty<RoleSummaryPbData>();
    /// <summary>缓存提供的不可变角色快照。</summary>
    public RoleWorldStatePbData Role => Current.Role;
    /// <summary>内部账号对象仅用于服务操作，不向节点暴露缓存容器。</summary>
    private GameSession Current => _current ?? throw new InvalidOperationException("尚未登录");

    /// <summary>建立先于界面的业务监听，网络由外部生命周期拥有。</summary>
    public SessionService(NetService net)
    {
        _net = net;
        _loginSubscription = net.Events.Subscribe<LoginRt>(OnLoggedIn, EventPhase.Cache);
    }

    /// <summary>发送认证请求，协议组装集中于业务服务。</summary>
    public void Login(string username, string password)
    {
        EnsureActive();
        Logout();
        _net.ResetSession();
        _net.Send(new Login { Username = username, Password = password });
    }

    /// <summary>保留账号摘要并提交选角。</summary>
    public void SelectRole(long roleId) { EnsureActive(); Current.SelectRole(roleId); }

    /// <summary>将逻辑坐标与移动序号交给绑定的账号会话。</summary>
    internal void SubmitMove(long actionId, Vector2 target) { EnsureActive(); Current.SubmitMove(actionId, target); }

    /// <summary>隔离旧角色消息，保留账号和登录摘要。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        if (_current is not null) _current.LeaveRole();
        else _net.ResetSession();
    }

    /// <summary>返回登录时释放整个账号状态。</summary>
    public void Logout()
    {
        EnsureActive();
        var current = _current;
        _current = null;
        current?.Dispose();
    }

    /// <summary>成功响应先创建独立账号状态，再允许界面读取。</summary>
    private void OnLoggedIn(LoginRt response)
    {
        if (!response.Success) return;
        if (_current is not null) return;
        _current = new GameSession(_net, response, false);
    }

    /// <summary>结束登录监听和账号缓存，不释放借用的网络。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loginSubscription.Dispose();
        var current = _current;
        _current = null;
        current?.Dispose();
    }

    /// <summary>阻止旧服务在重建后提交操作。</summary>
    private void EnsureActive() => ObjectDisposedException.ThrowIf(_disposed, this);
}
