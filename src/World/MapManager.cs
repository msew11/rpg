using System;
using System.Collections.Generic;
using Godot;
using Rpg.Config;

namespace Rpg.World;

/// <summary>封装地图坐标、边界、地块读取和可见范围计算。</summary>
public sealed class MapManager
{
    /// <summary>由稀疏覆盖格建立的快速查询表。</summary>
    private readonly Dictionary<(int X, int Y), string> _overrides = new();

    /// <summary>绑定本次启动的配置服务，不对外暴露依赖。</summary>
    private ConfigService Config { get; }

    /// <summary>构建稀疏地块索引。</summary>
    public MapManager(ConfigService config)
    {
        Config = config;
        foreach (var tile in config.MapConfig.Overrides)
            _overrides[(tile.X, tile.Y)] = tile.Tile;
    }

    /// <summary>将世界位置限制在地图范围内。</summary>
    public Vector2 Clamp(Vector2 position) => new(
        Mathf.Clamp(position.X, 0, Config.MapConfig.Width * Config.MapConfig.TileSize - 1),
        Mathf.Clamp(position.Y, 0, Config.MapConfig.Height * Config.MapConfig.TileSize - 1));

    /// <summary>按格坐标读取地块类型。</summary>
    public string TileAt(int x, int y) => _overrides.GetValueOrDefault((x, y), Config.MapConfig.DefaultTile);

    /// <summary>给出覆盖指定世界矩形的有效格子范围。</summary>
    public (int Left, int Top, int Right, int Bottom) VisibleTiles(Rect2 area)
    {
        var config = Config.MapConfig;
        var size = config.TileSize;
        return (
            Math.Clamp((int)Math.Floor(area.Position.X / size), 0, config.Width - 1),
            Math.Clamp((int)Math.Floor(area.Position.Y / size), 0, config.Height - 1),
            Math.Clamp((int)Math.Ceiling(area.End.X / size), 0, config.Width - 1),
            Math.Clamp((int)Math.Ceiling(area.End.Y / size), 0, config.Height - 1));
    }
}
