using System;
using Godot;

namespace Rpg.World;

/// <summary>一次绘制一个静态地图分块，并由画布复用绘制命令。</summary>
public partial class MapChunkView : Node2D
{
    /// <summary>分块读取的地图数据。</summary>
    private readonly MapManager _map;
    /// <summary>分块起始格的横向索引。</summary>
    private readonly int _left;
    /// <summary>分块起始格的纵向索引。</summary>
    private readonly int _top;
    /// <summary>分块的单边格子数。</summary>
    private readonly int _chunkSize;

    /// <summary>定位分块，并保留绘制所需的静态地图信息。</summary>
    public MapChunkView(MapManager map, int left, int top, int chunkSize)
    {
        _map = map;
        _left = left;
        _top = top;
        _chunkSize = chunkSize;
        var tileSize = map.Config.TileSize;
        Position = IsometricProjection.ToScreen(new Vector2(left * tileSize, top * tileSize));
    }

    /// <summary>只绘制地图范围内属于该分块的等轴地块。</summary>
    public override void _Draw()
    {
        var size = _map.Config.TileSize;
        var right = Math.Min(_left + _chunkSize, _map.Config.Width);
        var bottom = Math.Min(_top + _chunkSize, _map.Config.Height);
        for (var y = _top; y < bottom; y++)
        for (var x = _left; x < right; x++)
        {
            var accent = _map.TileAt(x, y) != _map.Config.DefaultTile;
            var checker = ((x + y) & 1) == 0;
            var color = accent ? new Color("607c96") : checker ? new Color("263a45") : new Color("2b414b");
            var origin = new Vector2((x - _left) * size, (y - _top) * size);
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
}
