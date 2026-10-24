using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>加载道具配置数组，并按 ItemCfg 标注的主键索引。</summary>
[CfgFile("item.json")]
public sealed class ItemCfgCache : MultiCfgCache<int, ItemCfg>
{
    /// <summary>由配置服务创建并拥有道具缓存。</summary>
    internal ItemCfgCache() { }
}
