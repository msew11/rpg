using System;
using System.Collections.Generic;
using Godot;
using Rpg.Net;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Cache;
using Rpg.Session.Data;

namespace Rpg.Session;

/// <summary>直接持有账号缓存，处理登录、选角、移动请求与账号生命周期。</summary>
public sealed class SessionService : Service
{
    /// <summary>本次启动绑定的网络。</summary>
    private readonly NetService _net;
    /// <summary>登录尝试时建立，由缓存自身处理响应的账号摘要。</summary>
    private LoginCache? _login;
    /// <summary>登录尝试时建立，选角后接纳状态的角色缓存。</summary>
    private RoleCache? _role;
    /// <summary>当前角色独立地图缓存，与 RoleCache 平级。</summary>
    private MapCache? _map;
    /// <summary>当前账号拥有的网络代次。</summary>
    private long _generation;

    /// <summary>账号是否已经认证。</summary>
    public bool IsLoggedIn => _login?.IsLoggedIn == true;
    /// <summary>角色缓存是否就绪。</summary>
    public bool HasRole => IsLoggedIn && _role?.IsInitialized == true;
    /// <summary>当前选择或等待进入的角色标识。</summary>
    public long RoleId => _role?.RoleId ?? 0;
    /// <summary>已转换的只读角色摘要。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles => _login?.Roles ?? Array.Empty<RoleSummaryPbData>();
    /// <summary>当前权威角色快照。</summary>
    public RoleWorldStatePbData Role => IsLoggedIn ? RoleCache.State : throw new InvalidOperationException("尚未登录");
    /// <summary>选角成功后的完整只读背包。</summary>
    public BagPbData Bag => HasRole ? RoleCache.Bag : throw new InvalidOperationException("尚未进入角色");
    /// <summary>当前地图的不可变对象快照，首次快照到达前为空。</summary>
    public DataMapAoiSnapshotPbData? Map => HasRole ? _map?.Snapshot : null;
    /// <summary>当前地图是否已经收到完整快照。</summary>
    public bool HasMap => Map is not null;
    /// <summary>账号缓存供缓存契约验证，节点通过 Roles 和 IsLoggedIn 读取。</summary>
    internal LoginCache LoginCache => _login ?? throw new InvalidOperationException("尚未登录");
    /// <summary>角色缓存供缓存契约验证，节点通过 Role 和 HasRole 读取。</summary>
    internal RoleCache RoleCache => _role ?? throw new InvalidOperationException("尚未登录");

    /// <summary>绑定本次启动的网络，不注册协议监听。</summary>
    public SessionService(NetService net)
    {
        ArgumentNullException.ThrowIfNull(net);
        _net = net;
        _generation = net.Generation;
    }

    /// <summary>从独立网络和已有响应建立账号，用于隔离协议测试。</summary>
    internal SessionService(NetService net, LoginRt response) : this(net)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Success) throw new InvalidOperationException("登录成功后才能建立账号");
        var login = new LoginCache(response);
        try
        {
            ResetNetwork();
            _role = new RoleCache();
            _map = new MapCache();
            _login = login;
        }
        catch { _map?.Dispose(); _role?.Dispose(); login.Dispose(); throw; }
    }

    /// <summary>结束旧账号并在请求前隔离网络代次。</summary>
    public void Login(string username, string password)
    {
        Logout();
        ResetNetwork();
        var login = new LoginCache();
        try
        {
            _role = new RoleCache();
            _map = new MapCache();
            _login = login;
            _net.Send(new Login { Username = username, Password = password });
        }
        catch
        {
            _map?.Dispose();
            _map = null;
            _role?.Dispose();
            login.Dispose();
            _role = null;
            _login = null;
            throw;
        }
    }

    /// <summary>清空旧角色并在新代次提交选角请求。</summary>
    public void SelectRole(long roleId)
    {
        EnsureActive();
        if (!IsLoggedIn) throw new InvalidOperationException("尚未登录");
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        var role = RoleCache;
        ResetNetwork();
        role.Reset(roleId);
        _map?.Reset(roleId);
        _net.Send(new SelectRole { RoleId = roleId });
    }

    /// <summary>清理角色数据与在途消息，保留账号摘要。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        if (_role is not null && RoleId == 0) return;
        ResetNetwork();
        _role?.Reset();
        _map?.Reset();
    }

    /// <summary>在选角列表提交创建，名称与外观由服务器最终校验。</summary>
    public void CreateRole(string name, RoleGender gender, int headId)
    {
        EnsureRoleList();
        _net.Send(new CreateRole { Name = name, Gender = gender switch
        {
            RoleGender.Male => RoleGenderPb.Male,
            RoleGender.Female => RoleGenderPb.Female,
            _ => RoleGenderPb.Unspecified
        }, HeadId = headId });
    }

    /// <summary>在选角列表提交删除指定角色。</summary>
    public void DeleteRole(long roleId)
    {
        EnsureRoleList();
        _net.Send(new DeleteRole { RoleId = roleId });
    }

    /// <summary>仅允许已认证且未选角的当前会话管理角色。</summary>
    private void EnsureRoleList()
    {
        EnsureActive();
        if (!IsLoggedIn || RoleId != 0) throw new InvalidOperationException("请在登录后的角色列表操作");
    }

    /// <summary>把当前角色移动意图提交到网络。</summary>
    internal void SubmitMove(long actionId, Vector2 target)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("选角成功后才能提交移动");
        _net.Send(new Move { RoleId = RoleId, ActionId = actionId, X = target.X, Y = target.Y });
    }

    /// <summary>提交去除开头斜杠的 GM 文本，最终参数与身份由服务端验证。</summary>
    public void SendGm(string command)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        _net.Send(new Gm { RoleId = RoleId, Command = command });
    }

    /// <summary>提交整份物品拖放，不预先修改缓存，服务端验证版本并决定合并或交换。</summary>
    public void MoveItem(long instanceId, int targetSlotIndex, long expectedRevision)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        _net.Send(new MoveItem { RoleId = RoleId, InstanceId = instanceId,
            TargetSlotIndex = targetSlotIndex, ExpectedRevision = expectedRevision });
    }

    /// <summary>提交穿戴或卸下，不预先改变背包与装备；实例零表示卸下目标槽位。</summary>
    public void EquipItem(long instanceId, Rpg.Const.EquipmentSlot slot, long expectedRevision, int? targetBagSlot = null)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        var request = new EquipItem { RoleId = RoleId, InstanceId = instanceId,
            Slot = (EquipmentSlotPb)slot, ExpectedRevision = expectedRevision };
        if (targetBagSlot is { } index) request.TargetBagSlot = index;
        _net.Send(request);
    }

    /// <summary>请求丢弃或销毁指定数量，不预先修改背包和地图缓存。</summary>
    public void DropItem(long instanceId, int count, long expectedRevision, bool destroy = false, Vector2? position = null)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        var request = new DropItem { RoleId = RoleId, InstanceId = instanceId, Count = count, ExpectedRevision = expectedRevision, Destroy = destroy };
        if (position is { } point) { request.DropX = point.X; request.DropY = point.Y; }
        _net.Send(request);
    }

    /// <summary>绑定已经提交的走近移动，服务端决定何时可以整份拾取。</summary>
    internal void PickupItem(Guid objectId, long objectRevision, long moveActionId)
    {
        EnsureActive();
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        _net.Send(new PickupItem { RoleId = RoleId, ObjectId = objectId.ToString("N"),
            ExpectedObjectRevision = objectRevision, ExpectedBagRevision = Bag.Revision, MoveActionId = moveActionId });
    }

    /// <summary>只为已发现缺口且没有在途请求的当前地图申请一次快照。</summary>
    public bool ResyncMapIfNeeded()
    {
        EnsureActive();
        if (!HasRole || _map is not { NeedsResync: true, ResyncPending: false }) return false;
        _net.Send(new ResyncMapAoi { RoleId = RoleId, MapId = Role.MapId });
        _map.MarkResyncPending();
        return true;
    }

    /// <summary>释放账号缓存，保留服务本身供重新登录。</summary>
    public void Logout()
    {
        EnsureActive();
        _map?.Dispose();
        _role?.Dispose();
        _login?.Dispose();
        _map = null;
        _role = null;
        _login = null;
    }

    /// <summary>更新服务拥有的代次，隔离先前消息。</summary>
    private void ResetNetwork()
    {
        _net.ResetSession();
        _generation = _net.Generation;
    }

    /// <summary>释放缓存及其监听；旧实例延后释放不重置后来建立的账号。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _map?.Dispose();
        _role?.Dispose();
        _login?.Dispose();
        if (_login is not null && !_net.IsDisposed && _net.Generation == _generation) _net.ResetSession();
    }

    /// <summary>阻止已释放或代次被替换的服务继续操作网络。</summary>
    private void EnsureActive()
    {
        ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(_net.IsDisposed, _net);
        if (_net.Generation != _generation) throw new InvalidOperationException("游戏会话已被替换");
    }
}
