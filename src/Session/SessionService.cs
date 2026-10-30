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
    /// <summary>显式 V2 传输使用账号管理与只读世界进入协议。</summary>
    private readonly bool _useV2;
    /// <summary>独立 V2 客户端同一时刻只允许一个账号请求。</summary>
    public bool AccountRequestPending => _login?.V2RequestPending == true;
    /// <summary>登录尝试时建立，由缓存自身处理响应的账号摘要。</summary>
    private LoginCache? _login;
    /// <summary>登录尝试时建立，选角后接纳状态的角色缓存。</summary>
    private RoleCache? _role;
    /// <summary>当前角色独立地图缓存，与 RoleCache 平级。</summary>
    private MapCache? _map;
    /// <summary>V2 完整响应的缓存阶段协调者，随账号释放。</summary>
    private WorldEntryCache? _entry;
    /// <summary>统一世界同步缓存。</summary>
    private WorldSyncCache? _sync;
    /// <summary>账号内 V2 物品组合接纳入口。</summary>
    private RoleItemsCache? _items;
    /// <summary>首帧后才可提交移动。</summary>
    public bool CanMove => HasRole && (!_useV2 || _sync?.Sequence > 0);
    /// <summary>当前公开远端角色。</summary>
    public IReadOnlyDictionary<long, DataPublicRole> RemoteRoles => _sync?.Roles ?? System.Collections.Frozen.FrozenDictionary<long, DataPublicRole>.Empty;
    /// <summary>当前世界帧序号。</summary>
    public ulong WorldSequence => _sync?.Sequence ?? 0;
    /// <summary>当前权威时间。</summary>
    public long ServerTimeMs => _sync?.ServerTimeMs ?? 0;
    /// <summary>是否使用 V2 权威同步。</summary>
    public bool UsesWorldFrames => _useV2;
    /// <summary>当前传输是否禁用角色物品写入。</summary>
    public bool IsWorldReadOnly => !_net.CanWriteItems;
    /// <summary>地面写操作尚待独立迁移。</summary>
    public bool CanWriteGroundItems => !_useV2;
    /// <summary>当前账号拥有的网络代次。</summary>
    private long _generation;

    /// <summary>账号是否已经认证。</summary>
    public bool IsLoggedIn => _login?.IsLoggedIn == true;
    /// <summary>角色缓存是否就绪。</summary>
    public bool HasRole => IsLoggedIn && _role?.IsInitialized == true;
    /// <summary>当前选择或等待进入的角色标识。</summary>
    public long RoleId => _role?.RoleId is > 0 ? _role.RoleId : _entry?.PendingRoleId ?? 0;
    /// <summary>已转换的只读角色摘要。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles => _login?.Roles ?? Array.Empty<RoleSummaryPbData>();
    /// <summary>当前权威角色快照。</summary>
    public RoleWorldStatePbData Role => IsLoggedIn ? RoleCache.State : throw new InvalidOperationException("尚未登录");
    /// <summary>选角成功后的完整只读背包。</summary>
    public BagPbData Bag => HasRole ? RoleCache.Bag : throw new InvalidOperationException("尚未进入角色");
    /// <summary>独立于私有背包的当前公开装备。</summary>
    public RoleWorldEquipmentPbData Equipment => HasRole ? RoleCache.Equipment : throw new InvalidOperationException("尚未进入角色");
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
        ObjectDisposedException.ThrowIf(net.IsDisposed, net);
        _net = net;
        _useV2 = net.IsV2;
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
        EnsureActive();
        if (AccountRequestPending) throw new InvalidOperationException("请等待当前账号操作完成");
        Logout();
        ResetNetwork();
        var login = new LoginCache(_useV2);
        try
        {
            _role = new RoleCache();
            _map = new MapCache();
            _login = login;
            if (_useV2) { _entry = new WorldEntryCache(login, _role, _map); _sync = new WorldSyncCache(_role, _map); _items = new RoleItemsCache(login, _role); }
            if (_useV2)
            {
                login.BeginV2Request<ProtocolV2.LoginRt>();
                _net.Send(new ProtocolV2.Login { Username = username, Password = password });
            }
            else _net.Send(new Login { Username = username, Password = password });
        }
        catch
        {
            _items?.Dispose();
        _sync?.Dispose();
            _entry?.Dispose();
            _items = null;
        _sync = null;
            _entry = null;
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
        if (_useV2)
        {
            if (!IsLoggedIn) throw new InvalidOperationException("尚未登录");
            if (AccountRequestPending) throw new InvalidOperationException("请等待当前账号操作完成");
            if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
            ResetNetwork();
            _login!.BeginV2Request<ProtocolV2.EnterWorldRt>();
            _sync!.Reset();
            _entry!.Begin(roleId);
            try { _net.Send(new ProtocolV2.EnterWorld { RoleId = roleId }); }
            catch { CancelAccountRequest(); throw; }
            return;
        }
        if (!IsLoggedIn) throw new InvalidOperationException("尚未登录");
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        var role = RoleCache;
        if (HasRole) _login!.RefreshRole(role.State);
        ResetNetwork();
        role.Reset(roleId);
        _map?.Reset(roleId);
        _net.Send(new SelectRole { RoleId = roleId });
    }

    /// <summary>清理角色数据与在途消息，V2 提交离开世界并保留账号摘要。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        if (_role is not null && RoleId == 0) return;
        if (HasRole && !_useV2) _login!.RefreshRole(RoleCache.State);
        _sync?.Reset();
        _entry?.Cancel();
        _login?.CancelV2Request();
        ResetNetwork();
        if (_useV2 && IsLoggedIn) _net.Send(new ProtocolV2.LeaveWorld());
        _role?.Reset();
        _map?.Reset();
    }

    /// <summary>在选角列表提交创建，名称与外观由服务器最终校验。</summary>
    public void CreateRole(string name, RoleGender gender, int headId)
    {
        EnsureRoleList();
        if (_useV2)
        {
            _login!.BeginV2Request<ProtocolV2.CreateRoleRt>();
            try { _net.Send(new ProtocolV2.CreateRole { Name = name, Gender = gender switch
                { RoleGender.Male => ProtocolV2.RoleGenderPb.Male, RoleGender.Female => ProtocolV2.RoleGenderPb.Female,
                    _ => ProtocolV2.RoleGenderPb.Unspecified }, HeadId = headId }); }
            catch { _login.CancelV2Request(); throw; }
            return;
        }
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
        if (_useV2)
        {
            _login!.BeginV2Request<ProtocolV2.DeleteRoleRt>();
            try { _net.Send(new ProtocolV2.DeleteRole { RoleId = roleId }); }
            catch { _login.CancelV2Request(); throw; }
            return;
        }
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
        if (!CanMove) throw new InvalidOperationException("首份世界快照后才能提交移动");
        if (_useV2) { _net.Send(new ProtocolV2.Move { ActionId = checked((ulong)actionId), Target = new() { X = target.X, Y = target.Y } }); return; }
        _net.Send(new Move { RoleId = RoleId, ActionId = actionId, X = target.X, Y = target.Y });
    }

    /// <summary>提交去除开头斜杠的 GM 文本，最终参数与身份由服务端验证。</summary>
    public void SendGm(string command)
    {
        EnsureActive();
        if (IsWorldReadOnly) throw new NotSupportedException("V2 世界当前仅供只读展示");
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        if (_useV2) { _net.Send(new ProtocolV2.Gm { Command = command }); return; }
        _net.Send(new Gm { RoleId = RoleId, Command = command });
    }

    /// <summary>提交整份物品拖放；V2 按最新状态执行，旧链路保留版本条件。</summary>
    public void MoveItem(long instanceId, int targetSlotIndex, long expectedRevision)
    {
        EnsureActive();
        if (IsWorldReadOnly) throw new NotSupportedException("V2 世界当前仅供只读展示");
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        if (_useV2) { _net.Send(new ProtocolV2.MoveItem { InstanceId = instanceId, TargetSlotIndex = targetSlotIndex }); return; }
        _net.Send(new MoveItem { RoleId = RoleId, InstanceId = instanceId,
            TargetSlotIndex = targetSlotIndex, ExpectedRevision = expectedRevision });
    }

    /// <summary>提交穿戴或卸下，不预先改变背包与装备；实例零表示卸下目标槽位。</summary>
    public void EquipItem(long instanceId, EquipmentSlot slot, long expectedBagRevision, long expectedEquipmentRevision, int? targetBagSlot = null)
    {
        EnsureActive();
        if (IsWorldReadOnly) throw new NotSupportedException("V2 世界当前仅供只读展示");
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        if (_useV2)
        {
            var v2 = new ProtocolV2.EquipItem { InstanceId = instanceId, Slot = (ProtocolV2.EquipmentSlotPb)slot };
            if (targetBagSlot is { } target) v2.TargetBagSlot = target;
            _net.Send(v2); return;
        }
        var request = new EquipItem { RoleId = RoleId, InstanceId = instanceId,
            Slot = (EquipmentSlotPb)slot, ExpectedBagRevision = expectedBagRevision, ExpectedEquipmentRevision = expectedEquipmentRevision };
        if (targetBagSlot is { } index) request.TargetBagSlot = index;
        _net.Send(request);
    }

    /// <summary>请求丢弃或销毁指定数量，不预先修改背包和地图缓存。</summary>
    public void DropItem(long instanceId, int count, long expectedRevision, bool destroy = false, Vector2? position = null)
    {
        EnsureActive();
        if (!CanWriteGroundItems) throw new NotSupportedException("V2 世界当前仅供只读展示");
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        var request = new DropItem { RoleId = RoleId, InstanceId = instanceId, Count = count, ExpectedRevision = expectedRevision, Destroy = destroy };
        if (position is { } point) { request.DropX = point.X; request.DropY = point.Y; }
        _net.Send(request);
    }

    /// <summary>绑定已经提交的走近移动，服务端决定何时可以整份拾取。</summary>
    internal void PickupItem(Guid objectId, long objectRevision, long moveActionId)
    {
        EnsureActive();
        if (!CanWriteGroundItems) throw new NotSupportedException("V2 世界当前仅供只读展示");
        if (!HasRole) throw new InvalidOperationException("请先进入角色");
        _net.Send(new PickupItem { RoleId = RoleId, ObjectId = objectId.ToString("N"),
            ExpectedObjectRevision = objectRevision, ExpectedBagRevision = Bag.Revision, MoveActionId = moveActionId });
    }

    /// <summary>只为已发现缺口且没有在途请求的当前地图申请一次快照。</summary>
    public bool ResyncMapIfNeeded()
    {
        EnsureActive();
        if (_useV2)
        {
            if (!HasRole || _sync is not { NeedsResync: true, ResyncPending: false }) return false;
            _net.Send(new ProtocolV2.ResyncWorld()); _sync.ResyncPending = true; return true;
        }
        if (!HasRole || _map is not { NeedsResync: true, ResyncPending: false }) return false;
        _net.Send(new ResyncMapAoi { RoleId = RoleId, MapId = Role.MapId });
        _map.MarkResyncPending();
        return true;
    }

    /// <summary>释放账号缓存，保留服务本身供重新登录。</summary>
    public void Logout()
    {
        EnsureActive();
        if (_useV2) ResetNetwork();
        _items?.Dispose();
        _sync?.Dispose();
        _entry?.Dispose();
        _map?.Dispose();
        _role?.Dispose();
        _login?.Dispose();
        _items = null;
        _sync = null;
        _entry = null;
        _map = null;
        _role = null;
        _login = null;
    }

    /// <summary>客户端网络或缓存异常释放 V2 请求等待，由 UI 展示错误。</summary>
    internal void CancelAccountRequest() { _entry?.Cancel(); _login?.CancelV2Request(); }

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
        _items?.Dispose();
        _sync?.Dispose();
        _entry?.Dispose();
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
