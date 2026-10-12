using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>处理测试怪物生成事件。</summary>
public partial class DebugMonsterSystem : EcsSystem
{
    /// <summary>此系统没有逐帧行为。</summary>
    public override void Update(EcsWorld world, double delta) { }

    /// <summary>在玩家附近且属于活跃区块的候选格创建怪物实体。</summary>
    [EcsEventHandler(typeof(SpawnDebugMonsterEvent))]
    private void OnSpawn(EcsWorld world, SpawnDebugMonsterEvent gameEvent)
    {
        var worldIds = world.Query(typeof(WorldChunksComponent));
        if (worldIds.Length == 0)
            return;
        var chunks = world.GetComponent<WorldChunksComponent>(worldIds[0])!;
        var chunkSize = chunks.chunk_size;
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
            return;
        var tile = WorldMath.TileAt(gameEvent.near_position);
        var raw = WorldMath.RawChunk(tile, chunkSize);
        if (!chunks.loaded.Contains(raw))
            return;
        var canonical = WorldMath.CanonicalChunk(raw, chunks.world_size);
        long parentId = 0;
        foreach (var id in world.Query(typeof(ChunkIdComponent)))
        {
            if (world.GetComponent<ChunkIdComponent>(id)!.value == canonical)
            {
                parentId = id;
                break;
            }
        }
        if (parentId == 0)
            return;
        var origin = new Vector2I(raw.X * chunkSize.X, raw.Y * chunkSize.Y);
        var candidates = new List<Vector2I>();
        for (var y = Math.Max(origin.Y, tile.Y - 2); y <= Math.Min(origin.Y + chunkSize.Y - 1, tile.Y + 2); y++)
        for (var x = Math.Max(origin.X, tile.X - 2); x <= Math.Min(origin.X + chunkSize.X - 1, tile.X + 2); x++)
        {
            var candidate = new Vector2I(x, y);
            if (candidate != tile)
                candidates.Add(candidate);
        }
        var selected = candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : tile;
        var position = new Vector2(selected.X + 0.5f, selected.Y + 0.5f);
        var entityId = world.CreateEntity(parentId);
        world.AddComponent(entityId, new MonsterComponent());
        world.AddComponent(entityId, new PositionComponent(position));
        gameEvent.entity_id = entityId;
        gameEvent.chunks_changed = true;
    }
}
