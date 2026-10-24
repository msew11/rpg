using System;
using Godot;
using Rpg.Config;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World.Actions;
using Rpg.World.Components;
using Rpg.World.Utils;

namespace Rpg.World;

/// <summary>内聚地图、ECS、输入意图和权威同步，节点仅负责呈现。</summary>
public sealed class WorldService : Service
{
    /// <summary>本次启动的账号服务。</summary>
    private readonly SessionService _session;
    /// <summary>本次启动的角色设置服务。</summary>
    private readonly SettingService _setting;
    /// <summary>供查询与世界初始化共用的只读地图数据。</summary>
    private readonly DataMapComponent _map;
    /// <summary>活动角色的 ECS 实例。</summary>
    private WorldManager? _world;
    /// <summary>当前启动内递增的移动意图序号。</summary>
    private long _nextActionId;
    /// <summary>世界更新所需的协议监听。</summary>
    private readonly IDisposable[] _subscriptions;

    /// <summary>地图世界是否就绪。</summary>
    public bool HasWorld => _world?.HasWorld == true;
    /// <summary>角色实体是否就绪。</summary>
    public bool HasRole => _world?.HasRole == true;
    /// <summary>用于摄像机和呈现的逻辑位置。</summary>
    public Vector2 RolePosition => _world?.RolePosition ?? Vector2.Zero;
    /// <summary>当前移动目标的只读快照。</summary>
    internal Vector2? RoleTarget => _world?.RoleTarget;
    /// <summary>角色动作与朝向的不可变快照。</summary>
    public DataActorState? ActorState => _world?.ActorState;

    /// <summary>显式绑定同次启动服务，并先于 UI 监听协议。</summary>
    public WorldService(ConfigService config, SettingService setting, SessionService session)
    {
        _session = session;
        _setting = setting;
        _map = MapUtils.CreateMap(config.MapConfig);
        _subscriptions = [GameServices.Events.Subscribe<SelectRoleRt>(OnSelectedRole),
            GameServices.Events.Subscribe<MoveRt>(OnMoved), GameServices.Events.Subscribe<RoleStateNotice>(OnRoleNotice)];
    }

    /// <summary>将未消费的逻辑坐标点击变成世界 Action 与网络请求。</summary>
    public bool HandleInput(InputEvent input, Vector2 logicalPosition)
    {
        ThrowIfDisposed();
        if (_world is null || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return false;
        var action = new MoveAction(++_nextActionId, MapUtils.Clamp(_map, logicalPosition));
        _session.SubmitMove(action.ActionId, action.Destination);
        _world.Enqueue(action);
        return true;
    }

    /// <summary>在网络推进之后按固定帧处理 ECS。</summary>
    public void Tick(double delta) { ThrowIfDisposed(); _world?.Tick(delta); }

    /// <summary>计算视口覆盖的地图格范围。</summary>
    public (int Left, int Top, int Right, int Bottom) VisibleTiles(Rect2 area) => MapUtils.VisibleTiles(_map, area);

    /// <summary>读取指定格的静态地块类型。</summary>
    public string TileAt(int x, int y) => MapUtils.TileAt(_map, x, y);

    /// <summary>移除 ECS 并保存、清除当前角色设置。</summary>
    public void LeaveRole()
    {
        ThrowIfDisposed();
        _world = null;
        _setting.LeaveRole();
    }

    /// <summary>选角成功后先建立地图实体，再建立角色实体与设置。</summary>
    private void OnSelectedRole(SelectRoleRt response)
    {
        if (!response.Success || response.RoleId != _session.RoleId) return;
        if (!_session.HasRole) throw new InvalidOperationException("选角响应未完成角色缓存初始化");
        _setting.SelectRole(_session.RoleId);
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(_map));
        world.Enqueue(new InitializeRoleAction(_session.Role));
        world.Tick(0);
        _world = world;
    }

    /// <summary>成功移动确认使用缓存阶段已更新的快照。</summary>
    private void OnMoved(MoveRt response)
    {
        if (response.Success && response.RoleId == _session.RoleId) SyncRole();
    }

    /// <summary>主动推送也通过 Action 更新世界。</summary>
    private void OnRoleNotice(RoleStateNotice notice)
    {
        if (notice.State?.RoleId == _session.RoleId) SyncRole();
    }

    /// <summary>不直接修改 ECS，只提交缓存快照。</summary>
    private void SyncRole()
    {
        if (_session.HasRole) _world?.Enqueue(new SyncRoleAction(_session.Role));
    }

    /// <summary>解除世界监听并移除所有角色实体。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _world = null;
    }

}
