using System.IO;
using System.Linq;
using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>加载单例地图并校验覆盖格的边界与唯一性。</summary>
[CfgFile("map.json")]
public sealed class MapCfgCache : SingleCfgCache<MapCfg>
{
    /// <summary>由配置服务创建并拥有地图缓存。</summary>
    internal MapCfgCache() { }

    /// <summary>补充 Schema 无法表达的跨字段地图约束。</summary>
    protected override void Validate(MapCfg value)
    {
        if (value.Overrides.Any(tile => tile.X >= value.Width || tile.Y >= value.Height))
            throw new InvalidDataException("覆盖格超出地图边界");
        if (value.Overrides.GroupBy(tile => (tile.X, tile.Y)).Any(group => group.Count() > 1))
            throw new InvalidDataException("覆盖格坐标重复");
    }
}
