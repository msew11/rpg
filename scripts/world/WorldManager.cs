using System.Collections.Generic;
using System.Linq;
using Godot;
using System;

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

    /// <summary>向外部提供区块状态的只读视图。</summary>
    private readonly WorldChunksView _chunksView;

    /// <summary>向外部提供调试状态的只读视图。</summary>
    private readonly WorldDebugView _debugView;

    /// <summary>标记会话是否已完成一次性世界初始化。</summary>
    private bool _sessionInitialized;

    /// <summary>管理活跃区块实体与临时落地。</summary>
    private readonly WorldChunkLifecycle _chunkLifecycle;

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

    /// <summary>临时落地失败时通知界面。</summary>
    [Signal] public delegate void system_errorEventHandler(string message);

    /// <summary>持有区块和调试组件的世界实体标识。</summary>
    public long world_entity_id { get; }

    /// <summary>创建世界实体及其组件，并按顺序注册移动与区块加载系统。</summary>
    public WorldManager(LocalDataRepoCache cache)
    {
        System.ArgumentNullException.ThrowIfNull(cache);
        _ecs = new EcsWorld(cache.WorldRepo.Data);
        world_entity_id = _ecs.CreateEntity();
        var chunksComponent = new WorldChunksComponent();
        var debugComponent = new WorldDebugComponent();
        _ecs.AddComponent(world_entity_id, chunksComponent);
        _ecs.AddComponent(world_entity_id, debugComponent);
        _chunksView = new WorldChunksView(chunksComponent);
        _debugView = new WorldDebugView(debugComponent);
        _chunkLifecycle = new WorldChunkLifecycle(_ecs, cache, world_entity_id, chunksComponent,
            message => EmitSignal("system_error", message));
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
        _chunkLifecycle.Sync(previousLoaded);
        EmitChangedSignals(previous);
    }

    /// <summary>收集活跃区块实体及其子树的落地内容。</summary>
    public string capture_chunk_entities(Vector2I raw) => _chunkLifecycle.Capture(raw);

    /// <summary>在读档前验证实体树和组件数据。</summary>
    public static bool can_restore_entities(string json) => WorldEntityTreeCodec.CanRestore(json);

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
        var entities = _chunkLifecycle.CaptureActiveEntities();
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
        _chunkLifecycle.SetConfig(cfg);
        var chunkSize = cfg.Get("chunk_size").AsVector2I();
        var worldSize = cfg.Get("world_size").AsVector2I();
        ChunksComponent().chunk_size = chunkSize;
        ChunksComponent().world_size = worldSize;
        ChunksComponent().active_chunk_size = cfg.Get("active_chunk_size").AsVector2I();
        step(0);
    }

    /// <summary>返回活跃区块实体；区块未激活时返回零。</summary>
    public long chunk_entity(Vector2I raw) => _chunkLifecycle.ChunkEntity(raw);

    /// <summary>读取所有活跃行走区块实例中的怪物显示位置。</summary>
    public Vector2[] visible_monster_positions() =>
        WorldMonsterQuery.VisiblePositions(_ecs, ChunksComponent(), _chunkLifecycle);

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
