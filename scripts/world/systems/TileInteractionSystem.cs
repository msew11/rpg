using Godot;

/// <summary>处理测试地块交互事件。</summary>
public partial class TileInteractionSystem : EcsSystem
{
    /// <summary>此系统没有逐帧行为。</summary>
    public override void Update(EcsWorld world, double delta) { }

    /// <summary>切换已加载地块并标记区块数据变化。</summary>
    [EcsEventHandler(typeof(ToggleTileEvent))]
    private void OnToggleTile(EcsWorld world, ToggleTileEvent gameEvent)
    {
        var worldIds = world.Query(typeof(WorldChunksComponent));
        if (worldIds.Length == 0)
            return;
        var chunks = world.GetComponent<WorldChunksComponent>(worldIds[0])!;
        if (chunks.chunk_size == Vector2I.Zero)
            return;
        var tile = WorldMath.TileAt(gameEvent.world_position);
        var canonical = WorldMath.CanonicalChunk(WorldMath.RawChunk(tile, chunks.chunk_size), chunks.world_size);
        if (!chunks.has_chunk(canonical))
            return;
        var local = WorldMath.LocalTile(tile, chunks.chunk_size);
        var tiles = chunks.tiles_for(canonical);
        var index = local.Y * chunks.chunk_size.X + local.X;
        tiles[index] = (byte)(1 - tiles[index]);
        gameEvent.chunks_changed = chunks.set_tiles(canonical, tiles);
    }
}
