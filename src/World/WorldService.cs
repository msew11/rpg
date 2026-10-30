using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Const;
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
    /// <summary>当前环境的移动速度，世界每帧读取。</summary>
    private readonly EnvService _env;
    /// <summary>当前世界使用的地图配置，无角色时不可读取。</summary>
    public MapCfg MapConfig => !IsDisposed && HasWorld && _session.HasRole && _config.Map.Items.TryGetValue(_session.Role.MapId, out var value)
        ? value : throw new InvalidOperationException("当前地图尚未初始化");
    /// <summary>从当前 ECS 实体读取动态地图对象。</summary>
    public DataWorldObjectsState? MapObjects => _world?.MapObjects;
    /// <summary>已提交 ECS 的最后一份 AOI 快照引用。</summary>
    private DataMapAoiSnapshotPbData? _lastMapSnapshot;
    /// <summary>上次同步的独立装备快照，固定帧只在引用变化时提交装备 Action。</summary>
    private RoleWorldEquipmentPbData? _lastEquipmentSnapshot;
    /// <summary>活动角色的 ECS 实例。</summary>
    private WorldManager? _world;
    /// <summary>当前启动内递增的移动意图序号。</summary>
    private long _nextActionId;

    /// <summary>最多保留尚未确认的发送意图，后一个目标替代前一个。</summary>
    private readonly SortedDictionary<long, MoveAction> _intentions = new();
    /// <summary>限流窗口内合并的最新目标。</summary>
    private MoveAction? _pendingMove;
    /// <summary>距离可发送下一次移动的时间。</summary>
    private double _sendCooldown;
    /// <summary>最后接纳世界帧。</summary>
    private ulong _worldSequence;
    /// <summary>当前帧接收后的客户端经过时间。</summary>
    private double _sinceFrame;
    /// <summary>远端角色有限插值缓冲。</summary>
    private readonly RemoteRoleBuffer _remotes = new();
    /// <summary>远端 ECS 呈现状态。</summary>
    public IReadOnlyList<DataActorState> RemoteActors => _world?.RemoteActors ?? Array.Empty<DataActorState>();
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
    public WorldService(EnvService env, ConfigService config, SettingService setting, SessionService session)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(session);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        ObjectDisposedException.ThrowIf(setting.IsDisposed, setting);
        ObjectDisposedException.ThrowIf(session.IsDisposed, session);
        _env = env;
        _session = session;
        _setting = setting;
        _config = config;
    }

    /// <summary>将未消费的逻辑坐标点击变成世界 Action 与网络请求。</summary>
    public bool HandleInput(InputEvent input, Vector2 logicalPosition)
    {
        ThrowIfDisposed();
        if (!_session.CanMove || _world is null || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return false;
        var action = new MoveAction(++_nextActionId, MapUtils.Clamp(RequireMap(), logicalPosition));
        if (_session.UsesWorldFrames)
        {
            _pendingMove = action; _intentions.Clear(); _intentions[action.ActionId] = action;
            SendPendingMove();
        }
        else _session.SubmitMove(action.ActionId, action.Destination);
        _world.Enqueue(action);
        return true;
    }

    /// <summary>按唯一地图快照选择物品，沿已有移动流程走近，再提交绑定此意图的拾取。</summary>
    public void PickupItem(Guid objectId)
    {
        ThrowIfDisposed();
        if (_session.IsWorldReadOnly) return;
        if (_world is null || MapObjects is not { } map || !map.Objects.TryGetValue(objectId, out var item) || item.MonsterTypeId is not null)
            throw new InvalidOperationException("地面物品已不存在");
        var action = new MoveAction(++_nextActionId, new Vector2(item.X, item.Y));
        _session.SubmitMove(action.ActionId, action.Destination);
        _world.Enqueue(action);
        _session.PickupItem(item.ObjectId, item.Revision, action.ActionId);
    }

    /// <summary>依据当前 ECS 地面物品选取分散落点；狭小地图交给服务端旧路径处理。</summary>
    public Vector2? SelectGroundDropPosition()
    {
        ThrowIfDisposed();
        if (!HasRole || _map is null || MapObjects is not { } objects)
            throw new InvalidOperationException("地面物品尚未同步");
        return SelectGroundDropPosition(RolePosition, _map, objects);
    }

    /// <summary>从指定世界快照选择落点，供正式世界和验收使用。</summary>
    internal static Vector2? SelectGroundDropPosition(Vector2 origin, DataMapComponent map, DataWorldObjectsState objects)
    {
        var occupied = new List<Vector2>();
        foreach (var item in objects.Objects.Values)
        {
            if (item.MonsterTypeId is not null) continue;
            var dx = item.X - origin.X;
            var dy = item.Y - origin.Y;
            var screen = new Vector2((dx - dy) * 0.5f, (dx + dy) * 0.25f);
            if (screen.LengthSquared() < 96f * 96f) occupied.Add(screen);
        }
        Span<Vector2> clear = stackalloc Vector2[68];
        var clearCount = 0;
        var best = origin;
        var bestDistance = -1f;
        var maxX = (long)map.Width * map.TileSize;
        var maxY = (long)map.Height * map.TileSize;
        for (var index = 0; index < 68; index++)
        {
            Vector2 screen;
            if (index < 64)
            {
                var angle = Random.Shared.NextSingle() * MathF.Tau;
                var radius = 48f + Random.Shared.NextSingle() * 8f;
                screen = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            }
            else screen = index switch
            {
                64 => new Vector2(0, 50),
                65 => new Vector2(-50, 0),
                66 => new Vector2(50, 0),
                _ => new Vector2(45, -25)
            };
            if (MathF.Abs(screen.X) < 40f && screen.Y < 24f) continue;
            var point = origin + new Vector2(screen.X + 2f * screen.Y, 2f * screen.Y - screen.X);
            if (point.X < 0 || point.Y < 0 || point.X >= maxX || point.Y >= maxY) continue;
            var nearest = float.PositiveInfinity;
            foreach (var other in occupied)
            {
                var dx = screen.X - other.X;
                var dy = screen.Y - other.Y;
                var squared = dx * dx + dy * dy;
                if (squared < nearest) nearest = squared;
            }
            if (nearest >= 40f * 40f) clear[clearCount++] = point;
            if (nearest > bestDistance) { bestDistance = nearest; best = point; }
        }
        if (clearCount > 0) return clear[Random.Shared.Next(clearCount)];
        return bestDistance >= 0 ? best : null;
    }

    /// <summary>在网络推进之后按固定帧处理 ECS。</summary>
    public void Tick(double delta)
    {
        ThrowIfDisposed();
        if (_world is null) return;
        if (_session.UsesWorldFrames && _session.HasRole)
        {
            _sendCooldown = Math.Max(0, _sendCooldown - delta); SendPendingMove();
            _sinceFrame += delta;
            if (_worldSequence != _session.WorldSequence)
            {
                _worldSequence = _session.WorldSequence; _sinceFrame = 0;
                foreach (var id in _intentions.Keys.Where(id => id <= _session.Role.AcknowledgedAction).ToArray()) _intentions.Remove(id);
                var target = _intentions.Count > 0 ? _intentions.Last().Value.Destination : _session.Role.Target;
                _world.Enqueue(new SyncMovementAction(_session.Role.Position, target));
                _remotes.Accept(_session.ServerTimeMs, _session.RemoteRoles);
            }
            _world.Enqueue(new SyncRemoteActorsAction(_remotes.Render(_session.ServerTimeMs + _sinceFrame * 1000 - 100)));
            _session.ResyncMapIfNeeded();
        }
        var snapshot = _session.Map;
        if (!ReferenceEquals(snapshot, _lastMapSnapshot))
        {
            _lastMapSnapshot = snapshot;
            _world.Enqueue(new SyncMapObjectsAction(snapshot));
        }
        SyncEquipment(_world);
        _world.Tick(delta);
    }

    /// <summary>每50ms最多发送最新目标，预测在点击时已经执行。</summary>
    private void SendPendingMove()
    {
        if (_pendingMove is not { } move || _sendCooldown > 0 || !_session.CanMove) return;
        _session.SubmitMove(move.ActionId, move.Destination); _pendingMove = null; _sendCooldown = .05;
    }
    /// <summary>切角清除旧意图、动作序号和远端样本。</summary>
    private void ResetMovement()
    { _nextActionId = 0; _intentions.Clear(); _pendingMove = null; _sendCooldown = 0; _worldSequence = 0; _sinceFrame = 0; _remotes.Clear(); }

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
        ResetMovement();
        _world = null;
        _lastMapSnapshot = null;
        _lastEquipmentSnapshot = null;
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
        ResetMovement();
        var world = new WorldManager(() => _session.UsesWorldFrames ? (int)_session.Role.Speed : _env.MovementSpeed);
        world.Enqueue(new InitializeWorldAction(_map!));
        world.Enqueue(new InitializeRoleAction(_session.Role));
        _lastMapSnapshot = _session.Map;
        if (_lastMapSnapshot is not null) world.Enqueue(new SyncMapObjectsAction(_lastMapSnapshot));
        _lastEquipmentSnapshot = null;
        SyncEquipment(world);
        world.Tick(0);
        _world = world;
    }

    /// <summary>不直接修改 ECS，只提交缓存快照。</summary>
    public void SyncRole()
    {
        ThrowIfDisposed();
        if (_session.HasRole) _world?.Enqueue(new SyncRoleAction(_session.Role));
    }

    /// <summary>从已验证的会话快照生成不可变装备动作，不在服务中订阅协议。</summary>
    private void SyncEquipment(WorldManager world)
    {
        if (!_session.HasRole || ReferenceEquals(_lastEquipmentSnapshot, _session.Equipment)) return;
        var equipment = _session.Equipment;
        var unchanged = _lastEquipmentSnapshot?.HasSameItems(equipment) == true;
        _lastEquipmentSnapshot = equipment;
        if (unchanged) return;
        world.Enqueue(new SyncEquipmentAction(_session.RoleId, equipment.Revision,
            equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand)?.ItemId ?? 0,
            equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.OffHand)?.ItemId ?? 0));
    }

    /// <summary>移除所有角色实体，不拥有协议监听。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        ResetMovement();
        _world = null;
        _lastMapSnapshot = null;
        _lastEquipmentSnapshot = null;
        _map = null;
    }

}
