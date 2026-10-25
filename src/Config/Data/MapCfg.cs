using System.Collections.Generic;
using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>地图尺寸、默认瓦片和稀疏覆盖配置。</summary>
public sealed class MapCfg
{
    /// <summary>地图标识。</summary>
    [CfgKey]
    public int Id { get; init; }
    /// <summary>网络兴趣块的单边格子数，独立于绘制分块。</summary>
    public int ChunkSizeTiles { get; init; } = 32;
    /// <summary>地图横向瓦片数。</summary>
    public int Width { get; init; }
    /// <summary>地图纵向瓦片数。</summary>
    public int Height { get; init; }
    /// <summary>每格的像素边长。</summary>
    public int TileSize { get; init; }
    /// <summary>未覆盖格子的地块类型。</summary>
    public string DefaultTile { get; init; } = "";
    /// <summary>少量不同于默认地块的格子。</summary>
    public List<TileOverride> Overrides { get; init; } = [];
}

/// <summary>单个地图瓦片的覆盖值。</summary>
public sealed class TileOverride
{
    /// <summary>横向格子索引。</summary>
    public int X { get; init; }
    /// <summary>纵向格子索引。</summary>
    public int Y { get; init; }
    /// <summary>该格的地块类型。</summary>
    public string Tile { get; init; } = "";
}
