namespace Rpg.Server.Modules.Role.Data;

/// <summary>装备实例扩展表；后续属性以此共享主键关联物品。</summary>
public sealed record DataEquipment
{
    /// <summary>与 DataItem 共用的主键。</summary>
    public long InstanceId { get; init; }
}
