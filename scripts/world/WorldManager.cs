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
    public EcsWorld ecs { get; }

    /// <summary>会话缓存负责临时存档读写。</summary>
    private readonly LocalDataRepoCache _cache;

    /// <summary>不可变测试地图配置，用于生成区块基准地块。</summary>
    private GodotObject? _cfg;

    /// <summary>活跃规范区块与其实体标识。</summary>
    private readonly Dictionary<Vector2I, long> _chunkEntities = [];

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
        ecs = new EcsWorld(cache.WorldRepo.Data);
        world_entity_id = ecs.CreateEntity();
        ecs.AddComponent(world_entity_id, new WorldChunksComponent());
        ecs.AddComponent(world_entity_id, new WorldDebugComponent());
        ecs.AddSystem(new MovementSystem());
        ecs.AddSystem(new ChunkLoadingSystem());
        ecs.AddSystem(new TileInteractionSystem());
        ecs.AddSystem(new DebugSettingsSystem());
        ecs.AddSystem(new DebugMonsterSystem());
    }

    /// <summary>创建具有位置、意图、速度与朝向的玩家实体。</summary>
    public long create_player(Vector2 position, float speed)
    {
        var entityId = ecs.CreateEntity(world_entity_id);
        ecs.AddComponent(entityId, new PositionComponent(position));
        ecs.AddComponent(entityId, new MovementIntentComponent());
        ecs.AddComponent(entityId, new MoveSpeedComponent(speed));
        ecs.AddComponent(entityId, new FacingComponent());
        ecs.AddComponent(entityId, new LocalPlayerComponent());
        EmitSignal("player_changed", entityId);
        return entityId;
    }

    /// <summary>推进 ECS，并在玩家位置或朝向变化时通知视图。</summary>
    public void step(double delta)
    {
        var previous = new Dictionary<long, (Vector2 Position, Vector2I Facing)>();
        var previousLoaded = chunks().loaded.ToArray();
        foreach (var entityId in ecs.Query(typeof(PositionComponent), typeof(FacingComponent)))
            previous[entityId] = (player_position(entityId), player_facing(entityId));
        ecs.Step(delta);
        SyncChunkEntities(previousLoaded);
        if (!chunks().loaded.SequenceEqual(previousLoaded))
            EmitSignal("chunks_changed");
        foreach (var (entityId, state) in previous)
        {
            if (player_position(entityId) != state.Position || player_facing(entityId) != state.Facing)
                EmitSignal("player_changed", entityId);
        }
    }

    /// <summary>在加载范围变化后卸载离开区块并创建新进入的区块。</summary>
    private void SyncChunkEntities(Vector2I[] previousLoaded)
    {
        if (_cfg is null)
            return;
        var current = chunks().loaded;
        foreach (var raw in previousLoaded)
        {
            if (current.Contains(raw))
                continue;
            var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
            if (!current.Any(candidate => WorldMath.CanonicalChunk(candidate, chunks().world_size) == canonical))
                unload_chunk_entity(raw);
        }
        foreach (var raw in current)
        {
            var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
            if (_chunkEntities.ContainsKey(canonical))
                continue;
            if (!chunks().has_chunk(canonical))
            {
                var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
                chunks().set_tiles(canonical, _cache.load_chunk(canonical, baseline));
            }
            var entityId = ecs.CreateEntity(world_entity_id);
            ecs.AddComponent(entityId, new ChunkIdComponent(canonical));
            _chunkEntities.Add(canonical, entityId);
            RestoreChunkTree(entityId, _cache.load_entities_json(canonical));
        }
    }

    /// <summary>落地一个不再活跃的区块及其子实体，然后释放内存；失败仍卸载。</summary>
    public bool unload_chunk_entity(Vector2I raw)
    {
        if (_cfg is null)
            return false;
        var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
        if (chunks().loaded.Any(candidate =>
                WorldMath.CanonicalChunk(candidate, chunks().world_size) == canonical))
            return false;
        if (!_chunkEntities.Remove(canonical, out var entityId))
            return false;
        var tiles = chunks().tiles_for(canonical);
        var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
        var persisted = _cache.unload_chunk(canonical, tiles, baseline, CaptureChunkTree(entityId));
        if (!persisted)
            EmitSignal("system_error", $"区块 {canonical.X},{canonical.Y} 临时存档写入失败，本次改动可能丢失。");
        chunks().remove_chunk(canonical);
        ecs.DestroyEntity(entityId);
        return persisted;
    }

    /// <summary>收集区块实体下所有子实体的可落地组件。</summary>
    public string capture_chunk_entities(Vector2I raw)
    {
        var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
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
        foreach (var (type, component) in ecs.Data.Components[entityId])
        {
            var data = component.ToPersistedString();
            if (data is not null)
                result.Components.Add(new PersistedComponentRecord { Type = type.FullName!, Data = data });
        }
        foreach (var childId in ecs.GetChildren(entityId))
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
        var entityId = ecs.CreateEntity(parentId);
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
            ecs.AddComponent(entityId, component);
        }
        foreach (var child in record.Children)
            RestoreEntity(entityId, child);
    }

    /// <summary>读取世界实体的区块组件。</summary>
    public WorldChunksComponent chunks() => ecs.GetComponent<WorldChunksComponent>(world_entity_id)!;

    /// <summary>读取世界实体的调试显示组件。</summary>
    public WorldDebugComponent debug() => ecs.GetComponent<WorldDebugComponent>(world_entity_id)!;

    /// <summary>读取玩家的连续世界位置。</summary>
    public Vector2 player_position(long entityId) => ecs.GetComponent<PositionComponent>(entityId)!.value;

    /// <summary>读取玩家当前保留的朝向。</summary>
    public Vector2I player_facing(long entityId) => ecs.GetComponent<FacingComponent>(entityId)!.value;

    /// <summary>将交互事件交给 ECS 路由，并通知对应的世界视图。</summary>
    public bool send_event(EcsEvent eventData)
    {
        var handled = ecs.SendEvent(eventData);
        if (eventData is WorldCommandEvent command)
        {
            if (command.chunks_changed)
                EmitSignal("chunks_changed");
            if (command.debug_changed)
                EmitSignal("debug_changed");
        }
        return handled;
    }

    /// <summary>设置玩家位置，并在有效变化时通知视图。</summary>
    public void set_player_position(long entityId, Vector2 value)
    {
        var position = ecs.GetComponent<PositionComponent>(entityId)!;
        if (position.value == value)
            return;
        position.value = value;
        EmitSignal("player_changed", entityId);
    }

    /// <summary>读取指定地块在规范区块中的当前类型。</summary>
    public int tile_type(Vector2I tile)
    {
        var chunkSize = chunks().chunk_size;
        var raw = WorldMath.RawChunk(tile, chunkSize);
        var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
        var local = WorldMath.LocalTile(tile, chunkSize);
        if (!chunks().has_chunk(canonical))
            return -1;
        return chunks().tiles_for(canonical)[local.Y * chunkSize.X + local.X];
    }

    /// <summary>绑定手工配置并按玩家当前位置首次加载区块。</summary>
    public void initialize_test_chunks(GodotObject cfg)
    {
        _cfg = cfg;
        var chunkSize = cfg.Get("chunk_size").AsVector2I();
        var worldSize = cfg.Get("world_size").AsVector2I();
        chunks().chunk_size = chunkSize;
        chunks().world_size = worldSize;
        chunks().active_chunk_size = cfg.Get("active_chunk_size").AsVector2I();
        step(0);
    }

    /// <summary>返回活跃区块实体；区块未激活时返回零。</summary>
    public long chunk_entity(Vector2I raw) =>
        _chunkEntities.GetValueOrDefault(WorldMath.CanonicalChunk(raw, chunks().world_size));

    /// <summary>读取所有活跃行走区块实例中的怪物显示位置。</summary>
    public Vector2[] visible_monster_positions()
    {
        var result = new List<Vector2>();
        var chunkSize = chunks().chunk_size;
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
            return result.ToArray();
        foreach (var raw in chunks().loaded)
        {
            var chunkId = chunk_entity(raw);
            if (chunkId == 0)
                continue;
            foreach (var childId in ecs.GetChildren(chunkId))
            {
                if (!ecs.HasComponent<MonsterComponent>(childId))
                    continue;
                var position = ecs.GetComponent<PositionComponent>(childId);
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
    public void set_player_facing(long entityId, Vector2I value)
    {
        var facing = ecs.GetComponent<FacingComponent>(entityId)!;
        if (facing.value == value)
            return;
        facing.value = value;
        EmitSignal("player_changed", entityId);
    }

    /// <summary>保存一个规范区块的地块，并通知依赖它的视图。</summary>
    public bool set_chunk_tiles(Vector2I canonicalId, byte[] tiles)
    {
        if (!chunks().set_tiles(canonicalId, tiles))
            return false;
        EmitSignal("chunks_changed");
        return true;
    }

}
