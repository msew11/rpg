using System.Collections.Generic;
using Godot;

namespace Rpg.World;

/// <summary>只保留视口附近的静态地图分块。</summary>
public partial class MapView : Node2D
{
    /// <summary>每个分块在横纵方向包含的格子数。</summary>
    private const int ChunkSize = 8;
    /// <summary>当前仍需显示的地图分块。</summary>
    private readonly Dictionary<Vector2I, MapChunkView> _chunks = new();
    /// <summary>上次更新时视口覆盖的分块范围。</summary>
    private (int Left, int Top, int Right, int Bottom)? _visibleChunks;

    /// <summary>地图读取与范围管理器。</summary>
    public MapManager? Map { get; set; }

    /// <summary>视口进入新的分块范围时增删分块，其余帧复用已有绘制。</summary>
    public override void _Process(double delta)
    {
        if (Map is null) return;
        var viewport = GetViewport();
        var inverse = viewport.GetCanvasTransform().AffineInverse();
        var screenSize = viewport.GetVisibleRect().Size;
        var corners = new[]
        {
            IsometricProjection.ToLogical(inverse * Vector2.Zero),
            IsometricProjection.ToLogical(inverse * new Vector2(screenSize.X, 0)),
            IsometricProjection.ToLogical(inverse * screenSize),
            IsometricProjection.ToLogical(inverse * new Vector2(0, screenSize.Y))
        };
        var minimum = corners[0];
        var maximum = corners[0];
        foreach (var corner in corners)
        {
            minimum = minimum.Min(corner);
            maximum = maximum.Max(corner);
        }
        var (left, top, right, bottom) = Map.VisibleTiles(new Rect2(minimum, maximum - minimum));
        (int Left, int Top, int Right, int Bottom) visible =
            (left / ChunkSize, top / ChunkSize, right / ChunkSize, bottom / ChunkSize);
        if (_visibleChunks == visible) return;

        var removed = new List<Vector2I>();
        foreach (var (coordinate, chunk) in _chunks)
        {
            if (coordinate.X >= visible.Left && coordinate.X <= visible.Right &&
                coordinate.Y >= visible.Top && coordinate.Y <= visible.Bottom) continue;
            removed.Add(coordinate);
            chunk.QueueFree();
        }
        foreach (var coordinate in removed) _chunks.Remove(coordinate);

        for (var y = visible.Top; y <= visible.Bottom; y++)
        for (var x = visible.Left; x <= visible.Right; x++)
        {
            var coordinate = new Vector2I(x, y);
            if (_chunks.ContainsKey(coordinate)) continue;
            var chunk = new MapChunkView(Map, x * ChunkSize, y * ChunkSize, ChunkSize)
            {
                Name = $"Chunk_{x}_{y}"
            };
            _chunks.Add(coordinate, chunk);
            AddChild(chunk);
        }

        _visibleChunks = visible;
    }
}
