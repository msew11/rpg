using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;

namespace Rpg.Server.Modules.World.Data;

/// <summary>从已加载配置复制的不可变地图规格，逻辑坐标以像素计。</summary>
/// <param name="MapId">地图身份。</param>
/// <param name="Width">横向格子数。</param>
/// <param name="Height">纵向格子数。</param>
/// <param name="TileSize">每格逻辑像素边长。</param>
/// <param name="ChunkSizeTiles">存储块的单边格子数。</param>
public sealed record DataWorldLayout([property: JsonRequired] int MapId, [property: JsonRequired] int Width,
    [property: JsonRequired] int Height, [property: JsonRequired] int TileSize, [property: JsonRequired] int ChunkSizeTiles)
{
    /// <summary>只做分块运算，不执行旧地图的边界或对象业务校验。</summary>
    internal DataWorldChunk ChunkAt(DataWorldObject value)
    {
        var size = (double)TileSize * ChunkSizeTiles;
        return new(unchecked((int)Math.Floor(value.X / size)), unchecked((int)Math.Floor(value.Y / size)));
    }
    /// <summary>保持块内对象顺序，统一默认数组处理，不引入坐标范围检查。</summary>
    internal Dictionary<DataWorldChunk, ImmutableArray<DataWorldObject>> Group(ImmutableArray<DataWorldObject> objects) =>
        (objects.IsDefault ? ImmutableArray<DataWorldObject>.Empty : objects).GroupBy(ChunkAt)
            .ToDictionary(group => group.Key, group => group.ToImmutableArray());
}
