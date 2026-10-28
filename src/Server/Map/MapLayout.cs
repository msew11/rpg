using System;
using System.Collections.Generic;
using System.IO;

namespace Rpg.Server.Map;

/// <summary>服务端独立地图规格；坐标使用未投影的逻辑像素。</summary>
/// <param name="MapId">数值地图身份。</param>
/// <param name="Width">横向格子数。</param>
/// <param name="Height">纵向格子数。</param>
/// <param name="TileSize">单格逻辑像素边长。</param>
/// <param name="ChunkSizeTiles">AOI 块的单边格子数。</param>
public sealed record MapLayout(int MapId, int Width, int Height, int TileSize, int ChunkSizeTiles)
{
    /// <summary>检查规格及乘积范围，拒绝无法寻址的地图。</summary>
    public void Validate()
    {
        if (MapId <= 0 || Width <= 0 || Height <= 0 || TileSize <= 0 || ChunkSizeTiles <= 0 ||
            (long)Width * TileSize > int.MaxValue || (long)Height * TileSize > int.MaxValue ||
            (long)ChunkSizeTiles * TileSize > int.MaxValue)
            throw new InvalidDataException("地图规格无效");
    }

    /// <summary>检查有限坐标及地图边界，不替客户端猜测合法位置。</summary>
    public bool Contains(float x, float y) => float.IsFinite(x) && float.IsFinite(y) && x >= 0 && y >= 0 &&
        x < (long)Width * TileSize && y < (long)Height * TileSize;

    /// <summary>验证后计算逻辑块坐标。</summary>
    public (int X, int Y) ChunkAt(float x, float y)
    {
        if (!Contains(x, y)) throw new InvalidDataException("位置超出地图边界");
        var size = (long)TileSize * ChunkSizeTiles;
        return ((int)Math.Floor(x / size), (int)Math.Floor(y / size));
    }

    /// <summary>列出中心周围九块，裁剪地图外范围。</summary>
    public HashSet<(int X, int Y)> Neighborhood(int x, int y)
    {
        var result = new HashSet<(int, int)>();
        var columns = ((long)Width + ChunkSizeTiles - 1) / ChunkSizeTiles;
        var rows = ((long)Height + ChunkSizeTiles - 1) / ChunkSizeTiles;
        if (x < 0 || y < 0 || x >= columns || y >= rows) throw new InvalidDataException("中心块越界");
        for (var cy = Math.Max(0, y - 1); cy <= Math.Min(rows - 1, (long)y + 1); cy++)
        for (var cx = Math.Max(0, x - 1); cx <= Math.Min(columns - 1, (long)x + 1); cx++) result.Add((cx, cy));
        return result;
    }
}
