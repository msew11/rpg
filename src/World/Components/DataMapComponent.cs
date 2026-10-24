namespace Rpg.World.Components;

/// <summary>世界实体持有的地图组件。</summary>
internal sealed class DataMapComponent
{
    /// <summary>实体关联的地图管理器。</summary>
    public MapManager Map { get; }

    /// <summary>按世界地图构造组件。</summary>
    public DataMapComponent(MapManager map)
    {
        Map = map;
    }
}
