using System;
using Godot;
using Rpg.World.Utils;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>一次绘制一个静态地图分块，并由画布复用绘制命令。</summary>
public partial class MapChunkView : Node2D
{
    /// <summary>分块起始格的横向索引。</summary>
    private readonly int _left;
    /// <summary>分块起始格的纵向索引。</summary>
    private readonly int _top;
    /// <summary>分块的单边格子数。</summary>
    private readonly int _chunkSize;
    /// <summary>整块地表共用一个带顶点颜色的二维网格。</summary>
    private ArrayMesh? _surface;
    /// <summary>整块地表边缘一次提交的线段端点。</summary>
    private Vector2[] _gridLines = [];

    /// <summary>定位分块，并保留绘制所需的静态地图信息。</summary>
    public MapChunkView(int left, int top, int chunkSize)
    {
        _left = left;
        _top = top;
        _chunkSize = chunkSize;
        var tileSize = GameServices.Config.MapConfig.TileSize;
        Position = IsometricProjection.ToScreen(new Vector2(left * tileSize, top * tileSize));
    }

    /// <summary>只绘制地图范围内属于该分块的等轴地块。</summary>
    public override void _Draw()
    {
        if (!GameServices.IsInitialized) return;
        if (_surface is null) BuildGeometry();
        DrawMesh(_surface!, null);
        DrawMultiline(_gridLines, new Color("38515b"), 1);
    }

    /// <summary>静态分块只构建一次几何数据，避免每个地块分别跨托管边界提交绘制。</summary>
    private void BuildGeometry()
    {
        var config = GameServices.Config.MapConfig;
        var size = config.TileSize;
        var right = Math.Min(_left + _chunkSize, config.Width);
        var bottom = Math.Min(_top + _chunkSize, config.Height);
        var count = (right - _left) * (bottom - _top);
        var vertices = new Vector2[count * 4];
        var colors = new Color[count * 4];
        var indices = new int[count * 6];
        _gridLines = new Vector2[count * 8];
        var tile = 0;
        for (var y = _top; y < bottom; y++)
        for (var x = _left; x < right; x++)
        {
            var accent = GameServices.World.TileAt(x, y) != config.DefaultTile;
            var checker = ((x + y) & 1) == 0;
            var color = accent ? new Color("607c96") : checker ? new Color("263a45") : new Color("2b414b");
            var origin = new Vector2((x - _left) * size, (y - _top) * size);
            var start = tile * 4;
            vertices[start] = IsometricProjection.ToScreen(origin);
            vertices[start + 1] = IsometricProjection.ToScreen(origin + new Vector2(size, 0));
            vertices[start + 2] = IsometricProjection.ToScreen(origin + new Vector2(size, size));
            vertices[start + 3] = IsometricProjection.ToScreen(origin + new Vector2(0, size));
            for (var corner = 0; corner < 4; corner++)
            {
                colors[start + corner] = color;
                _gridLines[tile * 8 + corner * 2] = vertices[start + corner];
                _gridLines[tile * 8 + corner * 2 + 1] = vertices[start + (corner + 1) % 4];
            }
            indices[tile * 6] = start;
            indices[tile * 6 + 1] = start + 1;
            indices[tile * 6 + 2] = start + 2;
            indices[tile * 6 + 3] = start;
            indices[tile * 6 + 4] = start + 2;
            indices[tile * 6 + 5] = start + 3;
            tile++;
        }
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        _surface = new ArrayMesh();
        _surface.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
            flags: Mesh.ArrayFormat.FlagUse2DVertices);
    }

    /// <summary>分块离开场景时释放所拥有的原生网格，重新入树可按需重建。</summary>
    public override void _ExitTree()
    {
        _surface?.Dispose();
        _surface = null;
        _gridLines = [];
    }
}
