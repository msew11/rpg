using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>根据本地玩家位置更新世界实体的附近行走区块。</summary>
public partial class ChunkLoadingSystem : EcsSystem
{
    /// <summary>在移动系统之后计算有限的区块显示范围。</summary>
    public override void Update(EcsWorld world, double delta)
    {
        var worldIds = world.Query(typeof(WorldChunksComponent));
        var playerIds = world.Query(typeof(PositionComponent), typeof(LocalPlayerComponent));
        if (worldIds.Length == 0 || playerIds.Length == 0)
            return;
        var chunks = world.GetComponent<WorldChunksComponent>(worldIds[0])!;
        if (chunks.chunk_size == Vector2I.Zero)
            return;
        var position = world.GetComponent<PositionComponent>(playerIds[0])!;
        var center = WorldMath.RawChunk(WorldMath.TileAt(position.value), chunks.chunk_size);
        var loaded = new Array<Vector2I>();
        var radiusX = chunks.active_chunk_size.X / 2;
        var radiusY = chunks.active_chunk_size.Y / 2;
        for (var y = center.Y - radiusY; y <= center.Y + radiusY; y++)
        for (var x = center.X - radiusX; x <= center.X + radiusX; x++)
            loaded.Add(new Vector2I(x, y));
        if (!chunks.loaded.SequenceEqual(loaded))
            chunks.loaded = loaded;
    }
}
