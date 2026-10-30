using System.Collections.Generic;
using Godot;
using Rpg.World.Utils;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>只保留视口附近的静态地图分块。</summary>
public partial class MapView : Node2D
{
    /// <summary>每个分块在横纵方向包含的格子数。</summary>
    private const int ChunkSize = 8;
    /// <summary>当前仍需显示的地图分块。</summary>
    private readonly Dictionary<Vector2I, MapChunkView> _chunks = new();
    /// <summary>按地图配置绘制逻辑 chunk 边界的 GM 覆盖层。</summary>
    private readonly ChunkBoundaryView _boundaries = new() { Visible = false };
    /// <summary>上次更新时视口覆盖的分块范围。</summary>
    private (int Left, int Top, int Right, int Bottom)? _visibleChunks;

    /// <summary>创建独立覆盖层，使边界线位于地表之上。</summary>
    public override void _Ready()
    {
        _boundaries.Name = "ChunkBoundaries";
        _boundaries.ZIndex = 1;
        AddChild(_boundaries);
    }

    /// <summary>即时更新逻辑 chunk 边界的显隐和颜色。</summary>
    public void SetChunkBoundaries(bool visible, Color color) => _boundaries.SetStyle(visible, color);

    /// <summary>视口进入新的分块范围时增删分块，其余帧复用已有绘制。</summary>
    public override void _Process(double delta)
    {
        if (!GameServices.IsInitialized || !GameServices.World.HasWorld) return;
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
        var (left, top, right, bottom) = GameServices.World.VisibleTiles(new Rect2(minimum, maximum - minimum));
        _boundaries.SetVisibleTiles(left, top, right, bottom);
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
            var chunk = new MapChunkView(x * ChunkSize, y * ChunkSize, ChunkSize)
            {
                Name = $"Chunk_{x}_{y}"
            };
            _chunks.Add(coordinate, chunk);
            AddChild(chunk);
        }

        _visibleChunks = visible;
    }
}
