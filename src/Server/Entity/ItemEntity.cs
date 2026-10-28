using System.Text.Json.Serialization;
using Rpg.Const;

namespace Rpg.Server.Entity;

/// <summary>物品多例表的一行，包含未穿与已穿实例。</summary>
public sealed record ItemEntity
{
    /// <summary>角色内主键，同时关联装备扩展表。</summary>
    [JsonRequired]
    public long InstanceId { get; init; }
    /// <summary>item.json 配置编号。</summary>
    [JsonRequired]
    public int ItemId { get; init; }
    /// <summary>背包格子，已穿实例固定为零。</summary>
    public int SlotIndex { get; init; }
    /// <summary>堆叠数量。</summary>
    [JsonRequired]
    public int Count { get; init; }
    /// <summary>是否穿戴。</summary>
    public bool Equipped { get; init; }
    /// <summary>服务端权威穿戴位置，不投影到客户端 ItemPb。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EquipmentSlot? EquipmentSlot { get; init; }
}
