using System.Collections.Generic;
using System.Linq;
using Godot;
using System;
using System.Text.Json;

/// <summary>管理世界实体和玩家实体，并向展示层发出状态变化通知。</summary>
[GlobalClass]
public partial class WorldManager : RefCounted
{
    /// <summary>供 GDScript 从缓存创建世界状态。</summary>
    public static WorldManager Create(LocalDataRepoCache cache) => new(cache);

    /// <summary>玩家位置或朝向变化时通知展示层。</summary>
    [Signal] public delegate void player_changedEventHandler(long entityId);

    /// <summary>区块内容或加载范围变化时通知展示层。</summary>
    [Signal] public delegate void chunks_changedEventHandler();

    /// <summary>调试开关变化时通知展示层。</summary>
    [Signal] public delegate void debug_changedEventHandler();

    /// <summary>保存实体组件并调度系统的 ECS 世界。</summary>
    private readonly EcsWorld _ecs;

    /// <summary>会话缓存负责临时存档读写。</summary>
    private readonly LocalDataRepoCache _cache;

    /// <summary>向外部提供区块状态的只读视图。</summary>
    private readonly WorldChunksView _chunksView;

    /// <summary>向外部提供调试状态的只读视图。</summary>
    private readonly WorldDebugView _debugView;

    /// <summary>不可变测试地图配置，用于生成区块基准地块。</summary>
    private GodotObject? _cfg;

    /// <summary>标记会话是否已完成一次性世界初始化。</summary>
    private bool _sessionInitialized;

    /// <summary>活跃规范区块与其实体标识。</summary>
    private readonly Dictionary<Vector2I, long> _chunkEntities = [];

    /// <summary>保存通知所需的可观察世界状态快照。</summary>
    private sealed class VisibleStateSnapshot
    {
        /// <summary>玩家位置与朝向。</summary>
        public Dictionary<long, (Vector2 Position, Vector2I Facing)> Players { get; init; } = [];

        /// <summary>区块尺寸与加载配置。</summary>
        public (Vector2I Chunk, Vector2I World, Vector2I Active) ChunkConfig { get; init; }

        /// <summary>当前加载的行走区块。</summary>
        public Vector2I[] Loaded { get; init; } = [];

        /// <summary>当前持有的规范区块地块副本。</summary>
        public Dictionary<Vector2I, byte[]> Tiles { get; init; } = [];

        /// <summary>活跃区块下的实体树数据。</summary>
        public Dictionary<Vector2I, string> Entities { get; init; } = [];

        /// <summary>调试视图的两个开关。</summary>
        public (bool Edges, bool Radar) Debug { get; init; }
    }

    /// <summary>记录一个组件的稳定类型名和落地内容。</summary>
    private sealed class PersistedComponentRecord
    {
        public string Type { get; set; } = "";
        public string Data { get; set; } = "";
    }

    /// <summary>记录区块下一个实体的组件和子实体。</summary>
    private sealed class PersistedEntityRecord
    {
        public List<PersistedComponentRecord> Components { get; set; } = [];
        public List<PersistedEntityRecord> Children { get; set; } = [];
    }

    /// <summary>临时落地失败时通知界面。</summary>
    [Signal] public delegate void system_errorEventHandler(string message);

    /// <summary>持有区块和调试组件的世界实体标识。</summary>
    public long world_entity_id { get; }

    /// <summary>创建世界实体及其组件，并按顺序注册移动与区块加载系统。</summary>
    public WorldManager(LocalDataRepoCache cache)
    {
        System.ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _ecs = new EcsWorld(cache.WorldRepo.Data);
        world_entity_id = _ecs.CreateEntity();
        var chunksComponent = new WorldChunksComponent();
        var debugComponent = new WorldDebugComponent();
        _ecs.AddComponent(world_entity_id, chunksComponent);
        _ecs.AddComponent(world_entity_id, debugComponent);
        _chunksView = new WorldChunksView(chunksComponent);
        _debugView = new WorldDebugView(debugComponent);
        _ecs.AddSystem(new MovementSystem());
        _ecs.AddSystem(new ChunkLoadingSystem());
        _ecs.AddSystem(new TileInteractionSystem());
        _ecs.AddSystem(new DebugSettingsSystem());
        _ecs.AddSystem(new DebugMonsterSystem());
    }

    /// <summary>按新游戏或有效存档的初始状态建立玩家和区块。</summary>
    public long initialize_session(Vector2 position, float speed, Vector2I facing, GodotObject cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (_sessionInitialized)
            throw new InvalidOperationException("World session has already been initialized.");
        var playerId = create_player(position, speed);
        set_player_facing(playerId, facing);
        initialize_test_chunks(cfg);
        _sessionInitialized = true;
        return playerId;
    }

    /// <summary>在初始化或测试布置时创建具有位置、意图、速度与朝向的玩家实体。</summary>
    internal long create_player(Vector2 position, float speed)
    {
        var entityId = _ecs.CreateEntity(world_entity_id);
        _ecs.AddComponent(entityId, new PositionComponent(position));
        _ecs.AddComponent(entityId, new MovementIntentComponent());
        _ecs.AddComponent(entityId, new MoveSpeedComponent(speed));
        _ecs.AddComponent(entityId, new FacingComponent());
        _ecs.AddComponent(entityId, new LocalPlayerComponent());
        EmitSignal("player_changed", entityId);
        return entityId;
    }

    /// <summary>推进 ECS，并通知实际改变的世界视图。</summary>
    public void step(double delta)
    {
        var previous = CaptureVisibleState();
        var previousLoaded = ChunksComponent().loaded.ToArray();
        _ecs.Step(delta);
        SyncChunkEntities(previousLoaded);
        EmitChangedSignals(previous);
    }

    /// <summary>在加载范围变化后卸载离开区块并创建新进入的区块。</summary>
    private void SyncChunkEntities(Vector2I[] previousLoaded)
    {
        if (_cfg is null)
            return;
        var current = ChunksComponent().loaded;
        foreach (var raw in previousLoaded)
        {
            if (current.Contains(raw))
                continue;
            var canonical = WorldMath.CanonicalChunk(raw, ChunksComponent().world_size);
            if (!current.Any(candidate => WorldMath.CanonicalChunk(candidate, ChunksComponent().world_size) == canonical))
                unload_chunk_entity(raw);
        }
        foreach (var raw in current)
        {
            var canonical = WorldMath.CanonicalChunk(raw, ChunksComponent().world_size);
            if (_chunkEntities.ContainsKey(canonical))
                continue;
            if (!ChunksComponent().has_chunk(canonical))
            {
                var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
                ChunksComponent().set_tiles(canonical, _cache.load_chunk(canonical, baseline));
            }
            var entityId = _ecs.CreateEntity(world_entity_id);
            _ecs.AddComponent(entityId, new ChunkIdComponent(canonical));
            _chunkEntities.Add(canonical, entityId);
            RestoreChunkTree(entityId, _cache.load_entities_json(canonical));
        }
    }

    /// <summary>落地一个不再活跃的区块及其子实体，然后释放内存；失败仍卸载。</summary>
    private bool unload_chunk_entity(Vector2I raw)
    {
        if (_cfg is null)
            return false;
        var canonical = WorldMath.CanonicalChunk(raw, ChunksComponent().world_size);
        if (ChunksComponent().loaded.Any(candidate =>
                WorldMath.CanonicalChunk(candidate, ChunksComponent().world_size) == canonical))
            return false;
        if (!_chunkEntities.Remove(canonical, out var entityId))
            return false;
        var tiles = ChunksComponent().tiles_for(canonical);
        var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
        var persisted = _cache.unload_chunk(canonical, tiles, baseline, CaptureChunkTree(entityId));
        if (!persisted)
            EmitSignal("system_error", $"区块 {canonical.X},{canonical.Y} 临时存档写入失败，本次改动可能丢失。");
        ChunksComponent().remove_chunk(canonical);
        _ecs.DestroyEntity(entityId);
        return persisted;
    }

    /// <summary>收集区块实体下所有子实体的可落地组件。</summary>
    public string capture_chunk_entities(Vector2I raw)
    {
        var canonical = WorldMath.CanonicalChunk(raw, ChunksComponent().world_size);
        return _chunkEntities.TryGetValue(canonical, out var entityId) ? CaptureChunkTree(entityId) : "{}";
    }

    /// <summary>序列化区块实体及其全部子树。</summary>
    private string CaptureChunkTree(long entityId)
    {
        var record = CaptureEntity(entityId);
        return record.Components.Count == 0 && record.Children.Count == 0
            ? "{}" : JsonSerializer.Serialize(record);
    }

    /// <summary>递归收集一个实体及其可落地组件。</summary>
    private PersistedEntityRecord CaptureEntity(long entityId)
    {
        var result = new PersistedEntityRecord();
        foreach (var (type, component) in _ecs.Data.Components[entityId])
        {
            var data = component.ToPersistedString();
            if (data is not null)
                result.Components.Add(new PersistedComponentRecord { Type = type.FullName!, Data = data });
        }
        foreach (var childId in _ecs.GetChildren(entityId))
            result.Children.Add(CaptureEntity(childId));
        return result;
    }

    /// <summary>根据临时存档重建区块实体及其子树状态。</summary>
    private void RestoreChunkTree(long chunkId, string json)
    {
        var record = JsonSerializer.Deserialize<PersistedEntityRecord>(json);
        if (record is not null)
            RestoreEntityData(chunkId, record);
    }

    /// <summary>在读档前验证实体子树及其组件落地字符串可还原。</summary>
    public static bool can_restore_entities(string json)
    {
        try
        {
            var record = JsonSerializer.Deserialize<PersistedEntityRecord>(json);
            var remaining = 10000;
            if (record is not null)
                ValidateEntity(record, 0, ref remaining);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>限制坏档的实体树深度并检查组件类型与数据。</summary>
    private static void ValidateEntity(PersistedEntityRecord record, int depth, ref int remaining)
    {
        if (depth > 64 || --remaining < 0 || record.Components is null || record.Children is null)
            throw new FormatException("Invalid persisted entity tree.");
        foreach (var entry in record.Components)
        {
            var type = ResolveComponentType(entry.Type);
            if (type is null || !typeof(EcsComponent).IsAssignableFrom(type) ||
                Activator.CreateInstance(type) is not EcsComponent component)
                throw new FormatException("Invalid persisted component type.");
            component.FromPersistedString(entry.Data);
            component.Dispose();
        }
        foreach (var child in record.Children)
            ValidateEntity(child, depth + 1, ref remaining);
    }

    /// <summary>仅从当前游戏程序集解析落地组件类型。</summary>
    private static Type? ResolveComponentType(string name) => typeof(WorldManager).Assembly.GetType(name);

    /// <summary>恢复一个子实体的组件及其后代。</summary>
    private void RestoreEntity(long parentId, PersistedEntityRecord record)
    {
        var entityId = _ecs.CreateEntity(parentId);
        RestoreEntityData(entityId, record);
    }

    /// <summary>恢复已创建实体的组件并递归创建其后代。</summary>
    private void RestoreEntityData(long entityId, PersistedEntityRecord record)
    {
        foreach (var entry in record.Components)
        {
            var type = ResolveComponentType(entry.Type);
            if (type is null || !typeof(EcsComponent).IsAssignableFrom(type) ||
                Activator.CreateInstance(type) is not EcsComponent component)
                throw new FormatException($"Invalid persisted component type: {entry.Type}");
            component.FromPersistedString(entry.Data);
            _ecs.AddComponent(entityId, component);
        }
        foreach (var child in record.Children)
            RestoreEntity(entityId, child);
    }

    /// <summary>内部读取世界实体的可变区块组件。</summary>
    private WorldChunksComponent ChunksComponent() => _ecs.GetComponent<WorldChunksComponent>(world_entity_id)!;

    /// <summary>读取当前区块状态的只读视图。</summary>
    public WorldChunksView chunks_view() => _chunksView;

    /// <summary>读取当前调试开关的只读视图。</summary>
    public WorldDebugView debug_view() => _debugView;

    /// <summary>读取玩家的连续世界位置。</summary>
    public Vector2 player_position(long entityId) => _ecs.GetComponent<PositionComponent>(entityId)!.value;

    /// <summary>读取玩家当前保留的朝向。</summary>
    public Vector2I player_facing(long entityId) => _ecs.GetComponent<FacingComponent>(entityId)!.value;

    /// <summary>路由交互事件并报告处理与变化结果，同时通知实际改变的视图。</summary>
    public WorldEventResult send_event(EcsEvent eventData)
    {
        var previous = CaptureVisibleState();
        var previousIntents = CaptureMovementIntents();
        var handled = _ecs.SendEvent(eventData);
        var visibleChanged = EmitChangedSignals(previous);
        var intentChanged = !SameEntries(previousIntents, CaptureMovementIntents(), (left, right) => left == right);
        return new WorldEventResult(handled, visibleChanged || intentChanged);
    }

    /// <summary>复制当前实体的移动意图供命令结果比较。</summary>
    private Dictionary<long, Vector2I> CaptureMovementIntents()
    {
        var result = new Dictionary<long, Vector2I>();
        foreach (var entityId in _ecs.Query(typeof(MovementIntentComponent)))
            result[entityId] = _ecs.GetComponent<MovementIntentComponent>(entityId)!.value;
        return result;
    }

    /// <summary>复制玩家、区块和调试视图依赖的状态。</summary>
    private VisibleStateSnapshot CaptureVisibleState()
    {
        var chunks = ChunksComponent();
        var debug = _ecs.GetComponent<WorldDebugComponent>(world_entity_id)!;
        var players = new Dictionary<long, (Vector2 Position, Vector2I Facing)>();
        foreach (var entityId in _ecs.Query(typeof(PositionComponent), typeof(FacingComponent)))
            players[entityId] = (player_position(entityId), player_facing(entityId));
        var tiles = new Dictionary<Vector2I, byte[]>();
        foreach (var canonical in chunks.chunk_ids())
            tiles[canonical] = chunks.tiles_for(canonical);
        var entities = new Dictionary<Vector2I, string>();
        foreach (var (canonical, entityId) in _chunkEntities)
            entities[canonical] = CaptureChunkTree(entityId);
        return new VisibleStateSnapshot
        {
            Players = players,
            ChunkConfig = (chunks.chunk_size, chunks.world_size, chunks.active_chunk_size),
            Loaded = chunks.loaded.ToArray(),
            Tiles = tiles,
            Entities = entities,
            Debug = (debug.edges, debug.radar)
        };
    }

    /// <summary>只为与快照不同的公开视图发出通知并报告是否有变化。</summary>
    private bool EmitChangedSignals(VisibleStateSnapshot previous)
    {
        var current = CaptureVisibleState();
        var changed = false;
        foreach (var entityId in previous.Players.Keys.Union(current.Players.Keys))
        {
            if (!previous.Players.TryGetValue(entityId, out var oldState) ||
                !current.Players.TryGetValue(entityId, out var newState) || oldState != newState)
            {
                EmitSignal("player_changed", entityId);
                changed = true;
            }
        }
        if (previous.ChunkConfig != current.ChunkConfig ||
            !previous.Loaded.SequenceEqual(current.Loaded) ||
            !SameEntries(previous.Tiles, current.Tiles, (left, right) => left.SequenceEqual(right)) ||
            !SameEntries(previous.Entities, current.Entities, (left, right) => left == right))
        {
            EmitSignal("chunks_changed");
            changed = true;
        }
        if (previous.Debug != current.Debug)
        {
            EmitSignal("debug_changed");
            changed = true;
        }
        return changed;
    }

    /// <summary>比较两份以相同键索引的状态快照。</summary>
    private static bool SameEntries<TKey, T>(Dictionary<TKey, T> left, Dictionary<TKey, T> right,
        Func<T, T, bool> equals) where TKey : notnull
    {
        if (left.Count != right.Count)
            return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) || !equals(value, other))
                return false;
        }
        return true;
    }

    /// <summary>设置玩家位置，并在有效变化时通知视图。</summary>
    internal void set_player_position(long entityId, Vector2 value)
    {
        var position = _ecs.GetComponent<PositionComponent>(entityId)!;
        if (position.value == value)
            return;
        position.value = value;
        EmitSignal("player_changed", entityId);
    }

    /// <summary>读取指定地块在规范区块中的当前类型。</summary>
    public int tile_type(Vector2I tile)
    {
        var chunkSize = ChunksComponent().chunk_size;
        var raw = WorldMath.RawChunk(tile, chunkSize);
        var canonical = WorldMath.CanonicalChunk(raw, ChunksComponent().world_size);
        var local = WorldMath.LocalTile(tile, chunkSize);
        if (!ChunksComponent().has_chunk(canonical))
            return -1;
        return ChunksComponent().tiles_for(canonical)[local.Y * chunkSize.X + local.X];
    }

    /// <summary>绑定手工配置并按玩家当前位置首次加载区块。</summary>
    internal void initialize_test_chunks(GodotObject cfg)
    {
        _cfg = cfg;
        var chunkSize = cfg.Get("chunk_size").AsVector2I();
        var worldSize = cfg.Get("world_size").AsVector2I();
        ChunksComponent().chunk_size = chunkSize;
        ChunksComponent().world_size = worldSize;
        ChunksComponent().active_chunk_size = cfg.Get("active_chunk_size").AsVector2I();
        step(0);
    }

    /// <summary>返回活跃区块实体；区块未激活时返回零。</summary>
    public long chunk_entity(Vector2I raw) =>
        _chunkEntities.GetValueOrDefault(WorldMath.CanonicalChunk(raw, ChunksComponent().world_size));

    /// <summary>读取所有活跃行走区块实例中的怪物显示位置。</summary>
    public Vector2[] visible_monster_positions()
    {
        var result = new List<Vector2>();
        var chunkSize = ChunksComponent().chunk_size;
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
            return result.ToArray();
        foreach (var raw in ChunksComponent().loaded)
        {
            var chunkId = chunk_entity(raw);
            if (chunkId == 0)
                continue;
            foreach (var childId in _ecs.GetChildren(chunkId))
            {
                if (!_ecs.HasComponent<MonsterComponent>(childId))
                    continue;
                var position = _ecs.GetComponent<PositionComponent>(childId);
                if (position is null)
                    continue;
                var originalRaw = WorldMath.RawChunk(WorldMath.TileAt(position.value), chunkSize);
                result.Add(position.value + new Vector2((raw.X - originalRaw.X) * chunkSize.X,
                    (raw.Y - originalRaw.Y) * chunkSize.Y));
            }
        }
        return result.ToArray();
    }

    /// <summary>设置玩家朝向，并在有效变化时通知视图。</summary>
    internal void set_player_facing(long entityId, Vector2I value)
    {
        var facing = _ecs.GetComponent<FacingComponent>(entityId)!;
        if (facing.value == value)
            return;
        facing.value = value;
        EmitSignal("player_changed", entityId);
    }

    /// <summary>保存一个规范区块的地块，并通知依赖它的视图。</summary>
    internal bool set_chunk_tiles(Vector2I canonicalId, byte[] tiles)
    {
        if (!ChunksComponent().set_tiles(canonicalId, tiles))
            return false;
        EmitSignal("chunks_changed");
        return true;
    }

}
