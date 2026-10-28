using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Net;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>私有完整物品快照，并提供背包格与已穿实例的只读视图。</summary>
public sealed class BagPbData
{
    /// <summary>最近接纳物品快照的信封版本。</summary>
    public long Revision { get; }
    /// <summary>全部持有实例，包括已穿物品。</summary>
    public IReadOnlyList<ItemPbData> AllItems { get; }
    /// <summary>未穿实例，供背包格绘制与操作。</summary>
    public IReadOnlyList<ItemPbData> Items { get; }
    /// <summary>私有实例与配置推导出的装备操作视图。</summary>
    public IReadOnlyList<EquippedItemData> Equipment { get; }

    /// <summary>拥有完整快照和两个互斥的派生视图。</summary>
    private BagPbData(long revision, List<ItemPbData> items, List<EquippedItemData> equipment)
    {
        Revision = revision;
        AllItems = items.AsReadOnly();
        Items = items.Where(item => !item.Equipped).ToList().AsReadOnly();
        Equipment = equipment.AsReadOnly();
    }

    /// <summary>验证实例唯一性、未穿格子、容量和装备公开规则。</summary>
    public static BagPbData FromPb(BagPb message, int capacity, IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message is null || message.GetRevision() < 0 || capacity <= 0) throw new InvalidDataException("背包协议或容量无效");
        var items = message.Items.Select(ItemPbData.FromPb).ToList();
        var bag = items.Where(item => !item.Equipped).ToList();
        if (items.Select(item => item.InstanceId).Distinct().Count() != items.Count ||
            bag.Select(item => item.SlotIndex).Distinct().Count() != bag.Count)
            throw new InvalidDataException("背包包含重复实例或格子");
        foreach (var item in items)
            if (!definitions.TryGetValue(item.ItemId, out var cfg) || item.Count > cfg.MaxStack ||
                !item.Equipped && item.SlotIndex >= capacity)
                throw new InvalidDataException("背包物品越界、配置不存在或数量超出上限");
        var equipment = items.Where(item => item.Equipped).Select(item => new EquippedItemData(
            item.InstanceId, item.ItemId, EquipmentDisplayData.FromItemId(item.ItemId, definitions).Slot)).ToList();
        var display = new RoleWorldEquipmentPb { RoleId = 1 };
        display.EquippedItems.AddRange(equipment.Select(item => item.ItemId));
        _ = RoleWorldEquipmentPbData.FromPb(display, definitions);
        return new(message.GetRevision(), items, equipment);
    }

    /// <summary>输出全部私有实例及新的扩展载荷。</summary>
    public BagPb ToPb()
    {
        var message = new BagPb();
        message.Items.AddRange(AllItems.Select(item => item.ToPb()));
        return message.WithRevision(Revision);
    }
}
