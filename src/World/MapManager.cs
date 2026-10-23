using System;
using System.Collections.Generic;
using Godot;
using Rpg.Config.Data;

namespace Rpg.World;

/// <summary>封装地图坐标、边界、地块读取和可见范围计算。</summary>
public sealed class MapManager
{
    /// <summary>由稀疏覆盖格建立的快速查询表。</summary>
    private readonly Dictionary<(int X, int Y), string> _overrides = new();

    /// <summary>该管理器使用的地图配置。</summary>
    public MapConfig Config { get; }

    /// <summary>构建稀疏地块索引。</summary>
    public MapManager(MapConfig config)
    {
        Config = config;
        foreach (var tile in config.Overrides)
            _overrides[(tile.X, tile.Y)] = tile.Tile;
    }

    /// <summary>将世界位置限制在地图范围内。</summary>
    public Vector2 Clamp(Vector2 position) => new(
        Mathf.Clamp(position.X, 0, Config.Width * Config.TileSize - 1),
        Mathf.Clamp(position.Y, 0, Config.Height * Config.TileSize - 1));

    /// <summary>按格坐标读取地块类型。</summary>
    public string TileAt(int x, int y) => _overrides.GetValueOrDefault((x, y), Config.DefaultTile);

    /// <summary>给出覆盖指定世界矩形的有效格子范围。</summary>
    public (int Left, int Top, int Right, int Bottom) VisibleTiles(Rect2 area)
    {
        var size = Config.TileSize;
        return (
            Math.Clamp((int)Math.Floor(area.Position.X / size), 0, Config.Width - 1),
            Math.Clamp((int)Math.Floor(area.Position.Y / size), 0, Config.Height - 1),
            Math.Clamp((int)Math.Ceiling(area.End.X / size), 0, Config.Width - 1),
            Math.Clamp((int)Math.Ceiling(area.End.Y / size), 0, Config.Height - 1));
    }
}
