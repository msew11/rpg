using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>根据本地玩家位置更新世界实体的附近行走区块。</summary>
public partial class ChunkLoadingSystem : EcsSystem
{
    /// <summary>玩家周围加载的区块半径。</summary>
    private const int LoadRadius = 1;

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
        for (var y = center.Y - LoadRadius; y <= center.Y + LoadRadius; y++)
        for (var x = center.X - LoadRadius; x <= center.X + LoadRadius; x++)
            loaded.Add(new Vector2I(x, y));
        if (!chunks.loaded.SequenceEqual(loaded))
            chunks.loaded = loaded;
    }
}
