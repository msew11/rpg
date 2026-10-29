using System.Text.Json.Serialization;
using Rpg.Const;

namespace Rpg.ServerV2.Modules.Role.Data;

/// <summary>物品多例表的一行，包含未穿与已穿实例。</summary>
public sealed record DataItem
{
    /// <summary>供 JSON 和对象初始化建立物品实体。</summary>
    public DataItem() { }

    /// <summary>建立一份未穿戴的背包物品。</summary>
    public DataItem(long instanceId, int itemId, int slotIndex, int count)
    {
        InstanceId = instanceId;
        ItemId = itemId;
        SlotIndex = slotIndex;
        Count = count;
    }

    /// <summary>建立已穿戴的单件物品，背包格子固定为零。</summary>
    public DataItem(long instanceId, int itemId, EquipmentSlot slot) : this(instanceId, itemId, 0, 1)
    {
        Equipped = true;
        EquipmentSlot = slot;
    }
    /// <summary>角色内主键，同时关联装备扩展表。</summary>
    public long InstanceId { get; init; }
    /// <summary>item.json 配置编号。</summary>
    public int ItemId { get; init; }
    /// <summary>背包格子，已穿实例固定为零。</summary>
    public int SlotIndex { get; init; }
    /// <summary>堆叠数量。</summary>
    public int Count { get; init; }
    /// <summary>是否穿戴。</summary>
    public bool Equipped { get; init; }
    /// <summary>服务端权威穿戴位置，不投影到客户端 ItemPb。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EquipmentSlot? EquipmentSlot { get; init; }
}
