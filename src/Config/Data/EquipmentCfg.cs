using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>独立装备外观关联及武器基础点伤、范围和攻速。</summary>
public sealed class EquipmentCfg
{
    /// <summary>装备配置主键，由道具 RelatedId 显式引用。</summary>
    [CfgKey]
    public int Id { get; init; }

    /// <summary>项目内持握资源编号，与道具及装备主键独立。</summary>
    public int AppearanceId { get; init; }
    /// <summary>启动时从原生持握资源解析的攻击类别，不属于JSON字段。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Rpg.Const.RoleMotion AttackMotion { get; internal set; }
    /// <summary>武器提供的整数点伤下限。</summary>
    public int BaseDamageMin { get; init; }
    /// <summary>武器提供的整数点伤上限。</summary>
    public int BaseDamageMax { get; init; }
    /// <summary>替换空手基础范围的逻辑像素距离；防具为零。</summary>
    public double BaseAttackRange { get; init; }
    /// <summary>替换空手基础攻速的每秒攻击次数；防具为零。</summary>
    public double BaseAttackSpeed { get; init; }
}
