using System.Collections.Frozen;

namespace Rpg.World.Components;

/// <summary>世界实体持有的只读地图数据。</summary>
internal sealed class DataMapComponent
{
    /// <summary>横向瓦片数。</summary>
    public int Width { get; }
    /// <summary>纵向瓦片数。</summary>
    public int Height { get; }
    /// <summary>每格的逻辑像素边长。</summary>
    public int TileSize { get; }
    /// <summary>未覆盖格子的地块类型。</summary>
    public string DefaultTile { get; }
    /// <summary>按格坐标索引的不可变稀疏地块数据。</summary>
    public FrozenDictionary<(int X, int Y), string> Overrides { get; }

    /// <summary>保存地图尺寸与已构建的不可变地块索引。</summary>
    public DataMapComponent(int width, int height, int tileSize, string defaultTile,
        FrozenDictionary<(int X, int Y), string> overrides)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        DefaultTile = defaultTile;
        Overrides = overrides;
    }
}
