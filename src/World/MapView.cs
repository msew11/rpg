using Godot;

namespace Rpg.World;

/// <summary>只为摄像机可见区域绘制地图地块。</summary>
public partial class MapView : Node2D
{
    /// <summary>地图读取与范围管理器。</summary>
    public MapManager? Map { get; set; }

    /// <summary>按当前视口重绘有限数量的瓦片。</summary>
    public override void _Draw()
    {
        if (Map is null) return;
        var viewport = GetViewport();
        var inverse = viewport.GetCanvasTransform().AffineInverse();
        var topLeft = inverse * Vector2.Zero;
        var bottomRight = inverse * viewport.GetVisibleRect().Size;
        var area = new Rect2(topLeft, bottomRight - topLeft);
        var (left, top, right, bottom) = Map.VisibleTiles(area);
        var size = Map.Config.TileSize;
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var accent = Map.TileAt(x, y) != Map.Config.DefaultTile;
            var checker = ((x + y) & 1) == 0;
            var color = accent ? new Color("607c96") : checker ? new Color("263a45") : new Color("2b414b");
            DrawRect(new Rect2(x * size, y * size, size, size), color);
            DrawRect(new Rect2(x * size, y * size, size, size), new Color("38515b"), false, 1);
        }
    }

    /// <summary>摄像机移动或视口变化时刷新可见地块。</summary>
    public override void _Process(double delta) => QueueRedraw();
}
