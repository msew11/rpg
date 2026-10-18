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
        var area = new Rect2(minimum, maximum - minimum);
        var (left, top, right, bottom) = Map.VisibleTiles(area);
        var size = Map.Config.TileSize;
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var accent = Map.TileAt(x, y) != Map.Config.DefaultTile;
            var checker = ((x + y) & 1) == 0;
            var color = accent ? new Color("607c96") : checker ? new Color("263a45") : new Color("2b414b");
            var origin = new Vector2(x * size, y * size);
            var points = new[]
            {
                IsometricProjection.ToScreen(origin),
                IsometricProjection.ToScreen(origin + new Vector2(size, 0)),
                IsometricProjection.ToScreen(origin + new Vector2(size, size)),
                IsometricProjection.ToScreen(origin + new Vector2(0, size))
            };
            DrawColoredPolygon(points, color);
            DrawPolyline([points[0], points[1], points[2], points[3], points[0]], new Color("38515b"), 1);
        }
    }

    /// <summary>摄像机移动或视口变化时刷新可见地块。</summary>
    public override void _Process(double delta) => QueueRedraw();
}
