using System;
using System.Collections.Generic;
using System.IO;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Utils;

namespace Rpg.Session.Data;

/// <summary>一件已完整验证的不可变装备实例。</summary>
/// <param name="InstanceId">角色内稳定物品编号。</param>
/// <param name="ItemId">道具配置编号。</param>
/// <param name="Slot">已适配的目标装备槽位。</param>
public sealed record EquippedItemPbData(long InstanceId, int ItemId, EquipmentSlot Slot)
{
    /// <summary>拒绝未知槽位、无效实例与分类不适配的装备。</summary>
    public static EquippedItemPbData FromPb(EquippedItemPb message, IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message is null || message.InstanceId <= 0 || !Enum.IsDefined((EquipmentSlot)message.Slot) ||
            !definitions.TryGetValue(message.ItemId, out var cfg) || cfg.MaxStack != 1 ||
            !EquipmentSlotUtil.CanEquip(cfg.Category, cfg.SubCategory, (EquipmentSlot)message.Slot))
            throw new InvalidDataException("装备协议包含无效实例、配置或槽位");
        return new(message.InstanceId, message.ItemId, (EquipmentSlot)message.Slot);
    }

    /// <summary>返回新的协议对象，避免共享可变传输数据。</summary>
    public EquippedItemPb ToPb() => new() { InstanceId = InstanceId, ItemId = ItemId, Slot = (EquipmentSlotPb)Slot };
}
