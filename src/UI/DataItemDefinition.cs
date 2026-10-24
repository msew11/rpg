namespace Rpg.UI;

/// <summary>保留导出清单中一条具名道具的分类、说明及候选图标，不代表已实现的玩法配置。</summary>
public sealed class DataItemDefinition
{
    /// <summary>来源表与行号组成的唯一键，允许道具ID重复。</summary>
    public string Key { get; set; } = "";
    /// <summary>源记录的原始道具编号。</summary>
    public int ItemId { get; set; }
    /// <summary>源记录中的中文名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>导出页面使用的父类型。</summary>
    public string Category { get; set; } = "";
    /// <summary>父类型的核对或推定依据。</summary>
    public string CategoryStatus { get; set; } = "";
    /// <summary>客户端背包大类，不直接等同于装备子类型。</summary>
    public string InventoryType { get; set; } = "";
    /// <summary>原始表中的类型码，在父类型内筛选。</summary>
    public int TypeCode { get; set; }
    /// <summary>来源提供的子类型名称。</summary>
    public string TypeName { get; set; } = "";
    /// <summary>子类型名称的确认程度。</summary>
    public string TypeStatus { get; set; } = "";
    /// <summary>与源记录匹配的道具说明。</summary>
    public string Description { get; set; } = "";
    /// <summary>原始来源表名。</summary>
    public string Source { get; set; } = "";
    /// <summary>来源表中的行号。</summary>
    public int SourceRow { get; set; }
    /// <summary>由候选路径精确关联的素材唯一键，空值表示无图。</summary>
    public string IconAssetId { get; set; } = "";
    /// <summary>导出清单的原始候选路径。</summary>
    public string IconCandidate { get; set; } = "";
    /// <summary>说明与道具记录的匹配方式。</summary>
    public string DescriptionMatch { get; set; } = "";
    /// <summary>说明匹配的候选条数。</summary>
    public int DescriptionCandidates { get; set; }
}
