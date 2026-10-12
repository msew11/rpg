using System.Collections.Generic;
using Godot;

/// <summary>只读计算活跃行走区块中怪物的回环显示位置。</summary>
internal static class WorldMonsterQuery
{
    /// <summary>读取所有活跃行走区块实例中的怪物显示位置。</summary>
    internal static Vector2[] VisiblePositions(EcsWorld ecs, WorldChunksComponent chunks, WorldChunkLifecycle lifecycle)
    {
        var result = new List<Vector2>();
        var chunkSize = chunks.chunk_size;
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
            return result.ToArray();
        foreach (var raw in chunks.loaded)
        {
            var chunkId = lifecycle.ChunkEntity(raw);
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
}
