using System.IO;
using Rpg.Protocol;

namespace Rpg.Session.Data;

/// <summary>与协议隔离的私有物品实例，穿戴标记与扩展类型一并复制。</summary>
/// <param name="InstanceId">角色内实例编号。</param>
/// <param name="ItemId">静态道具编号。</param>
/// <param name="SlotIndex">未穿物品的背包格，已穿为零。</param>
/// <param name="Count">堆叠数量。</param>
/// <param name="Equipped">当前是否穿戴。</param>
/// <param name="HasEquipment">是否携带装备扩展载荷。</param>
public sealed record ItemPbData(long InstanceId, int ItemId, int SlotIndex, int Count,
    bool Equipped = false, bool HasEquipment = false)
{
    /// <summary>直接验证并复制 结构，身份来自完整状态根节点。</summary>
    public static ItemPbData FromPb(global::Rpg.Protocol.ItemPb message)
    {
        if (message is null) throw new InvalidDataException("物品协议不能为空");
        var value = new ItemPbData(message.InstanceId, message.ItemId, message.SlotIndex,
            message.Count, message.Equipped, message.PayloadCase == global::Rpg.Protocol.ItemPb.PayloadOneofCase.Equipment);
        value.Validate();
        return value;
    }

    /// <summary>创建新的实例协议及独立的空装备扩展。</summary>
    public ItemPb ToPb()
    {
        Validate();
        var message = new ItemPb { InstanceId = InstanceId, ItemId = ItemId,
            SlotIndex = SlotIndex, Count = Count, Equipped = Equipped };
        if (HasEquipment) message.Equipment = new EquipmentPb();
        return message;
    }

    /// <summary>穿戴实例数量固定为一，不能泄漏装备槽位至格子编号。</summary>
    private void Validate()
    {
        if (InstanceId <= 0 || ItemId <= 0 || SlotIndex < 0 || Count <= 0 ||
            Equipped && (SlotIndex != 0 || Count != 1 || !HasEquipment))
            throw new InvalidDataException("物品实例字段或穿戴状态无效");
    }
}
