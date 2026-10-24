using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;
using Rpg.Config.Data;
using Rpg.World.Components;

namespace Rpg.World.Utils;

/// <summary>构建地图快照并提供无状态的地图查询与坐标计算。</summary>
internal static class MapUtils
{
    /// <summary>将已校验的配置复制为独立只读数据，仅索引稀疏覆盖格。</summary>
    public static DataMapComponent CreateMap(MapCfg config)
    {
        var overrides = new Dictionary<(int X, int Y), string>();
        foreach (var tile in config.Overrides)
            overrides[(tile.X, tile.Y)] = tile.Tile;
        return new DataMapComponent(config.Width, config.Height, config.TileSize,
            config.DefaultTile, overrides.ToFrozenDictionary());
    }

    /// <summary>将逻辑位置限制在地图有效像素范围内。</summary>
    public static Vector2 Clamp(DataMapComponent map, Vector2 position) => new(
        Mathf.Clamp(position.X, 0, map.Width * map.TileSize - 1),
        Mathf.Clamp(position.Y, 0, map.Height * map.TileSize - 1));

    /// <summary>按格坐标查询覆盖地块，否则使用默认地块。</summary>
    public static string TileAt(DataMapComponent map, int x, int y) =>
        map.Overrides.GetValueOrDefault((x, y), map.DefaultTile);

    /// <summary>计算逻辑视口矩形覆盖的有效格子范围。</summary>
    public static (int Left, int Top, int Right, int Bottom) VisibleTiles(DataMapComponent map, Rect2 area)
    {
        var size = map.TileSize;
        return (
            Math.Clamp((int)Math.Floor(area.Position.X / size), 0, map.Width - 1),
            Math.Clamp((int)Math.Floor(area.Position.Y / size), 0, map.Height - 1),
            Math.Clamp((int)Math.Ceiling(area.End.X / size), 0, map.Width - 1),
            Math.Clamp((int)Math.Ceiling(area.End.Y / size), 0, map.Height - 1));
    }
}
