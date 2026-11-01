using Godot;
using Rpg.Services;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>以细线显示当前地表绘制块集合的外边界。</summary>
public partial class RenderAreaBoundaryView : Node2D
{
    /// <summary>绘制块包围的逻辑格范围，右下界为开区间。</summary>
    private (int Left, int Top, int Right, int Bottom)? _tiles;
    /// <summary>当前画布相对屏幕的倍率，用于保持细线可见。</summary>
    private float _screenScale = 1f;
    /// <summary>调试边缘的当前颜色。</summary>
    public Color LineColor { get; private set; } = Colors.HotPink;

    /// <summary>相机倍率改变时保持边缘线约两屏幕像素宽。</summary>
    public void SetScreenScale(float value)
    {
        if (Mathf.IsEqualApprox(_screenScale, value)) return;
        _screenScale = value;
        if (Visible) QueueRedraw();
    }

    /// <summary>按已创建的绘制块更新边缘，避免额外显示范围扩大线框。</summary>
    public void SetChunks(int left, int top, int right, int bottom, int chunkSize)
    {
        var next = (left * chunkSize, top * chunkSize, (right + 1) * chunkSize, (bottom + 1) * chunkSize);
        if (_tiles == next) return;
        _tiles = next;
        if (Visible) QueueRedraw();
    }

    /// <summary>切换线框显隐和颜色。</summary>
    public void SetStyle(bool visible, Color color)
    {
        if (LineColor != color) { LineColor = color; QueueRedraw(); }
        Visible = visible;
    }

    /// <summary>将绘制块外边界裁剪到地图宽高并投影到等轴画布。</summary>
    public override void _Draw()
    {
        if (!Visible || _tiles is not { } tiles || !GameServices.IsInitialized || !GameServices.World.HasWorld) return;
        var config = GameServices.World.MapConfig;
        var size = config.TileSize;
        var left = tiles.Left * size;
        var top = tiles.Top * size;
        var right = Mathf.Min(tiles.Right, config.Width) * size;
        var bottom = Mathf.Min(tiles.Bottom, config.Height) * size;
        DrawPolyline(new[]
        {
            IsometricProjection.ToScreen(new Vector2(left, top)),
            IsometricProjection.ToScreen(new Vector2(right, top)),
            IsometricProjection.ToScreen(new Vector2(right, bottom)),
            IsometricProjection.ToScreen(new Vector2(left, bottom)),
            IsometricProjection.ToScreen(new Vector2(left, top))
        }, LineColor, 2f / Mathf.Max(_screenScale, 0.01f), true);
    }
}
