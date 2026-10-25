using System;
using System.IO;
using Rpg.Config.Data;
using Rpg.Session.Data;
using Godot;
using Rpg.Config;
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
    private DataMapComponent? _map;
    /// <summary>构造绑定地图配置，进入角色时按服务器身份选择。</summary>
    private readonly ConfigService _config;
    /// <summary>当前世界使用的地图配置，无角色时不可读取。</summary>
    public MapCfg MapConfig => !IsDisposed && HasWorld && _session.HasRole && _config.Map.Items.TryGetValue(_session.Role.MapId, out var value)
        ? value : throw new InvalidOperationException("当前地图尚未初始化");
    /// <summary>只读借用会话地图快照，不复制到 ECS。</summary>
    public DataMapAoiSnapshotPbData? MapObjects => HasRole ? _session.Map : null;
    /// <summary>活动角色的 ECS 实例。</summary>
    private WorldManager? _world;
    /// <summary>当前启动内递增的移动意图序号。</summary>
    private long _nextActionId;

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

    /// <summary>显式绑定同次启动服务，世界操作由 UI 协调调用。</summary>
    public WorldService(ConfigService config, SettingService setting, SessionService session)
    {
        _session = session;
        _setting = setting;
        _config = config;
    }

    /// <summary>将未消费的逻辑坐标点击变成世界 Action 与网络请求。</summary>
    public bool HandleInput(InputEvent input, Vector2 logicalPosition)
    {
        ThrowIfDisposed();
        if (_world is null || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return false;
        var action = new MoveAction(++_nextActionId, MapUtils.Clamp(RequireMap(), logicalPosition));
        _session.SubmitMove(action.ActionId, action.Destination);
        _world.Enqueue(action);
        return true;
    }

    /// <summary>按唯一地图快照选择物品，沿已有移动流程走近，再提交绑定此意图的拾取。</summary>
    public void PickupItem(Guid objectId)
    {
        ThrowIfDisposed();
        if (_world is null || MapObjects is not { } map || !map.Objects.TryGetValue(objectId, out var item) || item.MonsterTypeId is not null)
            throw new InvalidOperationException("地面物品已不存在");
        var action = new MoveAction(++_nextActionId, new Vector2(item.X, item.Y));
        _session.SubmitMove(action.ActionId, action.Destination);
        _world.Enqueue(action);
        _session.PickupItem(item.ObjectId, item.Revision, action.ActionId);
    }

    /// <summary>在网络推进之后按固定帧处理 ECS。</summary>
    public void Tick(double delta) { ThrowIfDisposed(); _world?.Tick(delta); }

    /// <summary>计算视口覆盖的地图格范围。</summary>
    public (int Left, int Top, int Right, int Bottom) VisibleTiles(Rect2 area) => MapUtils.VisibleTiles(RequireMap(), area);

    /// <summary>读取指定格的静态地块类型。</summary>
    public string TileAt(int x, int y) => MapUtils.TileAt(RequireMap(), x, y);

    /// <summary>地图查询只在世界初始化完成后开放，避免空引用和默认选图。</summary>
    private DataMapComponent RequireMap()
    {
        ThrowIfDisposed();
        return _map ?? throw new InvalidOperationException("当前地图尚未初始化");
    }

    /// <summary>移除 ECS 并保存、清除当前角色设置。</summary>
    public void LeaveRole()
    {
        ThrowIfDisposed();
        _world = null;
        _map = null;
        _setting.LeaveRole();
    }

    /// <summary>选角成功后先建立地图实体，再建立角色实体与设置。</summary>
    public void InitializeRole()
    {
        ThrowIfDisposed();
        if (!_session.HasRole) throw new InvalidOperationException("选角响应未完成角色缓存初始化");
        if (!_config.Map.Items.TryGetValue(_session.Role.MapId, out var config))
            throw new InvalidDataException("角色所属地图不存在");
        var map = MapUtils.CreateMap(config);
        _setting.SelectRole(_session.RoleId);
        _map = map;
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(_map!));
        world.Enqueue(new InitializeRoleAction(_session.Role));
        world.Tick(0);
        _world = world;
    }

    /// <summary>不直接修改 ECS，只提交缓存快照。</summary>
    public void SyncRole()
    {
        ThrowIfDisposed();
        if (_session.HasRole) _world?.Enqueue(new SyncRoleAction(_session.Role));
    }

    /// <summary>移除所有角色实体，不拥有协议监听。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _world = null;
        _map = null;
    }

}
