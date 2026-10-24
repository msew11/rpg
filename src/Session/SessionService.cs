using System;
using System.Collections.Generic;
using Godot;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Cache;
using Rpg.Session.Data;

namespace Rpg.Session;

/// <summary>直接持有账号缓存，处理登录、选角、移动请求与账号生命周期。</summary>
public sealed class SessionService : IService
{
    /// <summary>本次启动绑定的网络。</summary>
    private readonly NetService _net;
    /// <summary>在缓存阶段建立账号，失败时阻止 UI 成功回调。</summary>
    private readonly IDisposable _loginSubscription;
    /// <summary>登录后由服务内部建立的账号摘要。</summary>
    private LoginCache? _login;
    /// <summary>登录后由服务内部建立的角色缓存。</summary>
    private RoleCache? _role;
    /// <summary>当前账号拥有的网络代次。</summary>
    private long _generation;
    /// <summary>是否已结束服务。</summary>
    private bool _disposed;

    /// <summary>账号是否已经认证。</summary>
    public bool IsLoggedIn => _login?.IsLoggedIn == true;
    /// <summary>角色缓存是否就绪。</summary>
    public bool HasRole => _role?.IsInitialized == true;
    /// <summary>当前选择或等待进入的角色标识。</summary>
    public long RoleId => _role?.RoleId ?? 0;
    /// <summary>已转换的只读角色摘要。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles => _login?.Roles ?? Array.Empty<RoleSummaryPbData>();
    /// <summary>当前权威角色快照。</summary>
    public RoleWorldStatePbData Role => RoleCache.State;
    /// <summary>账号缓存供缓存契约验证，节点通过 Roles 和 IsLoggedIn 读取。</summary>
    internal LoginCache LoginCache => _login ?? throw new InvalidOperationException("尚未登录");
    /// <summary>角色缓存供缓存契约验证，节点通过 Role 和 HasRole 读取。</summary>
    internal RoleCache RoleCache => _role ?? throw new InvalidOperationException("尚未登录");

    /// <summary>绑定网络并建立常驻登录监听，缓存随后在登录成功时初始化。</summary>
    public SessionService(NetService net)
    {
        ArgumentNullException.ThrowIfNull(net);
        _net = net;
        _generation = net.Generation;
        _loginSubscription = GameServices.Events.Subscribe<LoginRt>(OnLoggedIn, EventPhase.Cache);
    }

    /// <summary>从全局入口绑定网络并装入已有响应，供隔离的会话验收使用。</summary>
    internal SessionService(LoginRt response) : this(GameServices.Net, response) { }

    /// <summary>从独立网络和已有响应建立账号，用于隔离协议测试。</summary>
    internal SessionService(NetService net, LoginRt response) : this(net)
    {
        try { InitializeAccount(response, true); }
        catch { _loginSubscription.Dispose(); throw; }
    }

    /// <summary>结束旧账号并在请求前隔离网络代次。</summary>
    public void Login(string username, string password)
    {
        Logout();
        ResetNetwork();
        _net.Send(new Login { Username = username, Password = password });
    }

    /// <summary>清空旧角色并在新代次提交选角请求。</summary>
    public void SelectRole(long roleId)
    {
        EnsureActive();
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        var role = RoleCache;
        ResetNetwork();
        role.Reset(roleId);
        _net.Send(new SelectRole { RoleId = roleId });
    }

    /// <summary>清理角色数据与在途消息，保留账号摘要。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        if (_role is not null && RoleId == 0) return;
        ResetNetwork();
        _role?.Reset();
    }

    /// <summary>把当前角色移动意图提交到网络。</summary>
    internal void SubmitMove(long actionId, Vector2 target)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("选角成功后才能提交移动");
        _net.Send(new Move { RoleId = RoleId, ActionId = actionId, X = target.X, Y = target.Y });
    }

    /// <summary>释放账号缓存，保留服务本身供重新登录。</summary>
    public void Logout()
    {
        EnsureActive();
        _role?.Dispose();
        _login?.Dispose();
        _role = null;
        _login = null;
    }

    /// <summary>首次成功响应建立缓存；后续摘要由已有 LoginCache 处理。</summary>
    private void OnLoggedIn(LoginRt response)
    {
        if (response.Success && _login is null) InitializeAccount(response, false);
    }

    /// <summary>完整校验摘要后建立账号，失败时回收已创建的订阅。</summary>
    private void InitializeAccount(LoginRt response, bool resetNetwork)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Success) throw new InvalidOperationException("登录成功后才能建立账号");
        var login = new LoginCache(response);
        try
        {
            if (resetNetwork) ResetNetwork();
            _role = new RoleCache();
            _login = login;
        }
        catch { login.Dispose(); throw; }
    }

    /// <summary>更新服务拥有的代次，隔离先前消息。</summary>
    private void ResetNetwork()
    {
        _net.ResetSession();
        _generation = _net.Generation;
    }

    /// <summary>释放监听和缓存；旧实例延后释放不重置后来建立的账号。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loginSubscription.Dispose();
        _role?.Dispose();
        _login?.Dispose();
        if (_login is not null && !_net.IsDisposed && _net.Generation == _generation) _net.ResetSession();
    }

    /// <summary>阻止已释放或代次被替换的服务继续操作网络。</summary>
    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_net.IsDisposed, _net);
        if (_net.Generation != _generation) throw new InvalidOperationException("游戏会话已被替换");
    }
}
