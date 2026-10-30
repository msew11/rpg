using Godot;
using Rpg.Services;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>在地表上绘制当前可见的逻辑 chunk 外框，供 GM 检查地图分块。</summary>
public partial class ChunkBoundaryView : Node2D
{
    /// <summary>当前视口覆盖的逻辑格范围。</summary>
    private (int Left, int Top, int Right, int Bottom)? _visibleTiles;
    /// <summary>边界线颜色，默认绿色。</summary>
    public Color LineColor { get; private set; } = Colors.Green;

    /// <summary>设置可见格范围，仅在范围变化时重画。</summary>
    public void SetVisibleTiles(int left, int top, int right, int bottom)
    {
        var next = (left, top, right, bottom);
        if (_visibleTiles == next) return;
        _visibleTiles = next;
        if (Visible) QueueRedraw();
    }

    /// <summary>切换边界线及颜色，不改动地表分块。</summary>
    public void SetStyle(bool visible, Color color)
    {
        if (LineColor != color)
        {
            LineColor = color;
            QueueRedraw();
        }
        Visible = visible;
    }

    /// <summary>按地图配置的 chunk 大小绘制裁剪到地图边缘的等轴外框。</summary>
    public override void _Draw()
    {
        if (!Visible || _visibleTiles is not { } tiles || !GameServices.IsInitialized || !GameServices.World.HasWorld) return;
        var config = GameServices.World.MapConfig;
        var chunkSize = config.ChunkSizeTiles;
        var tileSize = config.TileSize;
        for (var y = tiles.Top / chunkSize; y <= tiles.Bottom / chunkSize; y++)
        for (var x = tiles.Left / chunkSize; x <= tiles.Right / chunkSize; x++)
        {
            var left = x * chunkSize * tileSize;
            var top = y * chunkSize * tileSize;
            var right = Mathf.Min((x + 1) * chunkSize, config.Width) * tileSize;
            var bottom = Mathf.Min((y + 1) * chunkSize, config.Height) * tileSize;
            var points = new[]
            {
                IsometricProjection.ToScreen(new Vector2(left, top)),
                IsometricProjection.ToScreen(new Vector2(right, top)),
                IsometricProjection.ToScreen(new Vector2(right, bottom)),
                IsometricProjection.ToScreen(new Vector2(left, bottom)),
                IsometricProjection.ToScreen(new Vector2(left, top))
            };
            DrawPolyline(points, LineColor, 1, true);
        }
    }
}
