using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>独立装备配置的关联入口，具体武器属性按后续需求扩展。</summary>
public sealed class EquipmentCfg
{
    /// <summary>装备配置主键，由道具 RelatedId 显式引用。</summary>
    [CfgKey]
    public int Id { get; init; }
}
