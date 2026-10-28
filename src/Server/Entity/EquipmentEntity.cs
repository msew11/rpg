using System.Text.Json.Serialization;

namespace Rpg.Server.Entity;

/// <summary>装备实例扩展表；后续属性以此共享主键关联物品。</summary>
public sealed record EquipmentEntity
{
    /// <summary>与 ItemEntity 共用的主键。</summary>
    [JsonRequired]
    public long InstanceId { get; init; }
}
