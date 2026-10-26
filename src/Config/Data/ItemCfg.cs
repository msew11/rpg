using Rpg.Config.Cache;
using Rpg.Const;

namespace Rpg.Config.Data;

/// <summary>单条道具的分类和堆叠上限配置。</summary>
public sealed class ItemCfg
{
    /// <summary>道具唯一编号，也是配置映射的键。</summary>
    [CfgKey]
    public int Id { get; init; }
    /// <summary>用于展示和识别的道具名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>道具所属的已定义大类。</summary>
    public required ItemCategory Category { get; init; }
    /// <summary>道具小类原码，必须与所属大类匹配。</summary>
    public required ItemSubCategory SubCategory { get; init; }
    /// <summary>所属类别专用配置的关联编号，装备引用 EquipmentCfg.Id。</summary>
    public required int RelatedId { get; init; }
    /// <summary>单个堆叠允许的最大数量。</summary>
    public int MaxStack { get; init; }
    /// <summary>客户端图集素材键，空值或未找到时显示占位图。</summary>
    public string IconAssetId { get; init; } = "";
    /// <summary>独立地面素材键，缺失时显示占位，不复用背包图标。</summary>
    public string GroundAssetId { get; init; } = "";
}
