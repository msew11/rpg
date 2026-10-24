using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>单条道具的分类和堆叠上限配置。</summary>
public sealed class ItemCfg
{
    /// <summary>道具唯一编号，也是配置映射的键。</summary>
    [CfgKey]
    public int Id { get; init; }
    /// <summary>用于展示和识别的道具名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>道具大类编号，具体分类含义由配置确定。</summary>
    public int Category { get; init; }
    /// <summary>道具小类编号，归属当前大类。</summary>
    public int SubCategory { get; init; }
    /// <summary>单个堆叠允许的最大数量。</summary>
    public int MaxStack { get; init; }
    /// <summary>客户端图集素材键，空值或未找到时显示占位图。</summary>
    public string IconAssetId { get; init; } = "";
}
