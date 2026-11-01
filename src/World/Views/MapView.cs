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
    /// <summary>按当前权威订阅快照高亮 AOI 的 GM 覆盖层。</summary>
    private readonly AoiHighlightView _aoi = new() { Visible = false };
    /// <summary>标出当前实际创建的地表绘制块外沿。</summary>
    private readonly RenderAreaBoundaryView _renderArea = new() { Visible = false };
    /// <summary>仅影响显示、不参与地表分块选择的 GM 倍率。</summary>
    private float _gmZoom = 1f;
    /// <summary>上次更新时视口覆盖的分块范围。</summary>
    private (int Left, int Top, int Right, int Bottom)? _visibleChunks;

    /// <summary>创建独立覆盖层，使边界线位于地表之上。</summary>
    public override void _Ready()
    {
        _boundaries.Name = "ChunkBoundaries";
        _boundaries.ZIndex = 1;
        AddChild(_boundaries);
        _aoi.Name = "AoiHighlight";
        _aoi.ZIndex = 2;
        AddChild(_aoi);
        _renderArea.Name = "RenderAreaBoundary";
        _renderArea.ZIndex = 3;
        AddChild(_renderArea);
    }

    /// <summary>即时更新逻辑 chunk 边界的显隐和颜色。</summary>
    public void SetChunkBoundaries(bool visible, Color color) => _boundaries.SetStyle(visible, color);

    /// <summary>切换当前已接纳 AOI 块的 GM 高亮。</summary>
    public void SetAoiHighlight(bool visible) => _aoi.SetHighlightVisible(visible);

    /// <summary>更新当前世界的 GM 倍率，地表可见范围仍按基础缩放计算。</summary>
    public void SetGmZoom(float value) => _gmZoom = Mathf.Clamp(value, 0.1f, 4f);

    /// <summary>切换绘制区域外沿及其颜色。</summary>
    public void SetRenderAreaBoundary(bool visible, Color color) => _renderArea.SetStyle(visible, color);

    /// <summary>视口进入新的分块范围时增删分块，其余帧复用已有绘制。</summary>
    public override void _Process(double delta)
    {
        if (!GameServices.IsInitialized || !GameServices.World.HasWorld) return;
        var viewport = GetViewport();
        var inverse = viewport.GetCanvasTransform().AffineInverse();
        _aoi.SetSnapshot(GameServices.Session.Map);
        _aoi.SetScreenScale(viewport.GetCanvasTransform().X.Length());
        _renderArea.SetScreenScale(viewport.GetCanvasTransform().X.Length());
        var screenSize = viewport.GetVisibleRect().Size;
        var screenCenter = screenSize / 2;
        var worldCenter = inverse * screenCenter;
        var corners = new[]
        {
            IsometricProjection.ToLogical(worldCenter + (inverse * Vector2.Zero - worldCenter) * _gmZoom),
            IsometricProjection.ToLogical(worldCenter + (inverse * new Vector2(screenSize.X, 0) - worldCenter) * _gmZoom),
            IsometricProjection.ToLogical(worldCenter + (inverse * screenSize - worldCenter) * _gmZoom),
            IsometricProjection.ToLogical(worldCenter + (inverse * new Vector2(0, screenSize.Y) - worldCenter) * _gmZoom)
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
        _renderArea.SetChunks(visible.Left, visible.Top, visible.Right, visible.Bottom, ChunkSize);
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
