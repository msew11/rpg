using System;
using System.Collections.Generic;
using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>客户端独立保存服务器地图规格，避免依赖 Server 类型。</summary>
/// <param name="Width">横向格子数。</param>
/// <param name="Height">纵向格子数。</param>
/// <param name="TileSize">格子逻辑像素边长。</param>
/// <param name="ChunkSizeTiles">AOI 块的单边格子数。</param>
public sealed record DataMapLayoutPbData(int Width, int Height, int TileSize, int ChunkSizeTiles)
{
    /// <summary>检查规格和乘积边界并转换。</summary>
    public static DataMapLayoutPbData FromPb(MapLayoutPb? value)
    {
        if (value is null || value.Width <= 0 || value.Height <= 0 || value.TileSize <= 0 || value.ChunkSizeTiles <= 0 ||
            (long)value.Width * value.TileSize > int.MaxValue || (long)value.Height * value.TileSize > int.MaxValue ||
            (long)value.ChunkSizeTiles * value.TileSize > int.MaxValue) throw new InvalidDataException("地图同步规格无效");
        return new(value.Width, value.Height, value.TileSize, value.ChunkSizeTiles);
    }

    /// <summary>计算对象所属块，越界或非有限坐标明确报错。</summary>
    public (int X, int Y) ChunkAt(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < 0 || y < 0 ||
            x >= (long)Width * TileSize || y >= (long)Height * TileSize) throw new InvalidDataException("地图对象位置越界");
        var size = (long)TileSize * ChunkSizeTiles;
        return ((int)Math.Floor(x / size), (int)Math.Floor(y / size));
    }

    /// <summary>校验中心并构造有效九块集合。</summary>
    public HashSet<(int X, int Y)> Neighborhood((int X, int Y) center)
    {
        var columns = ((long)Width + ChunkSizeTiles - 1) / ChunkSizeTiles;
        var rows = ((long)Height + ChunkSizeTiles - 1) / ChunkSizeTiles;
        if (center.X < 0 || center.Y < 0 || center.X >= columns || center.Y >= rows) throw new InvalidDataException("地图中心块越界");
        var result = new HashSet<(int, int)>();
        for (var y = Math.Max(0, center.Y - 1); y <= Math.Min(rows - 1, (long)center.Y + 1); y++)
        for (var x = Math.Max(0, center.X - 1); x <= Math.Min(columns - 1, (long)center.X + 1); x++) result.Add((x, y));
        return result;
    }
}
