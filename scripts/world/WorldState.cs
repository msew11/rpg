using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>管理世界实体和玩家实体，并向展示层发出状态变化通知。</summary>
[GlobalClass]
public partial class WorldState : RefCounted
{
    /// <summary>玩家位置或朝向变化时通知展示层。</summary>
    [Signal] public delegate void player_changedEventHandler(long entityId);

    /// <summary>区块内容或加载范围变化时通知展示层。</summary>
    [Signal] public delegate void chunks_changedEventHandler();

    /// <summary>调试开关变化时通知展示层。</summary>
    [Signal] public delegate void debug_changedEventHandler();

    /// <summary>保存实体组件并调度系统的 ECS 世界。</summary>
    public EcsWorld ecs { get; } = new();

    /// <summary>持有区块和调试组件的世界实体标识。</summary>
    public long world_entity_id { get; }

    /// <summary>创建世界实体及其组件，并按顺序注册移动与区块加载系统。</summary>
    public WorldState()
    {
        world_entity_id = ecs.CreateEntity();
        ecs.AddComponent(world_entity_id, new WorldChunksComponent());
        ecs.AddComponent(world_entity_id, new WorldDebugComponent());
        ecs.AddSystem(new MovementSystem());
        ecs.AddSystem(new ChunkLoadingSystem());
    }

    /// <summary>创建具有位置、意图、速度与朝向的玩家实体。</summary>
    public long create_player(Vector2 position, float speed)
    {
        var entityId = ecs.CreateEntity();
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
        if (!chunks().loaded.SequenceEqual(previousLoaded))
            EmitSignal("chunks_changed");
        foreach (var (entityId, state) in previous)
        {
            if (player_position(entityId) != state.Position || player_facing(entityId) != state.Facing)
                EmitSignal("player_changed", entityId);
        }
    }

    /// <summary>读取世界实体的区块组件。</summary>
    public WorldChunksComponent chunks() => ecs.GetComponent<WorldChunksComponent>(world_entity_id)!;

    /// <summary>读取世界实体的调试显示组件。</summary>
    public WorldDebugComponent debug() => ecs.GetComponent<WorldDebugComponent>(world_entity_id)!;

    /// <summary>读取玩家的连续世界位置。</summary>
    public Vector2 player_position(long entityId) => ecs.GetComponent<PositionComponent>(entityId)!.value;

    /// <summary>读取玩家当前保留的朝向。</summary>
    public Vector2I player_facing(long entityId) => ecs.GetComponent<FacingComponent>(entityId)!.value;

    /// <summary>设置玩家移动意图，供下一次 ECS 更新使用。</summary>
    public void set_player_intent(long entityId, Vector2I direction) =>
        ecs.GetComponent<MovementIntentComponent>(entityId)!.value = direction;

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
        return chunks().tiles_for(canonical)[local.Y * chunkSize.X + local.X];
    }

    /// <summary>切换指定世界位置的测试地块类型。</summary>
    public void toggle_tile(Vector2 worldPosition)
    {
        var tile = WorldMath.TileAt(worldPosition);
        var chunkSize = chunks().chunk_size;
        var raw = WorldMath.RawChunk(tile, chunkSize);
        var canonical = WorldMath.CanonicalChunk(raw, chunks().world_size);
        var local = WorldMath.LocalTile(tile, chunkSize);
        var tiles = chunks().tiles_for(canonical);
        var index = local.Y * chunkSize.X + local.X;
        tiles[index] = (byte)(1 - tiles[index]);
        set_chunk_tiles(canonical, tiles);
    }

    /// <summary>从 GDScript 手工配置一次写入固定测试目录。</summary>
    public void initialize_test_chunks(GodotObject cfg)
    {
        var chunkSize = cfg.Get("chunk_size").AsVector2I();
        var worldSize = cfg.Get("world_size").AsVector2I();
        chunks().chunk_size = chunkSize;
        chunks().world_size = worldSize;
        for (var y = 0; y < worldSize.Y; y++)
        for (var x = 0; x < worldSize.X; x++)
        {
            var canonical = new Vector2I(x, y);
            set_chunk_tiles(canonical, cfg.Call("tiles_for", canonical).AsByteArray());
        }
        step(0);
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

    /// <summary>切换区块边缘显示，并通知调试视图。</summary>
    public void set_debug_edges(bool enabled)
    {
        if (debug().edges == enabled)
            return;
        debug().edges = enabled;
        EmitSignal("debug_changed");
    }

    /// <summary>切换雷达显示，并通知调试视图。</summary>
    public void set_debug_radar(bool enabled)
    {
        if (debug().radar == enabled)
            return;
        debug().radar = enabled;
        EmitSignal("debug_changed");
    }
}
