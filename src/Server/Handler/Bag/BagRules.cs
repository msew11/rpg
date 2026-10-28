using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Utils;

namespace Rpg.Server.Handler.Bag;

/// <summary>处理器共用的背包规则，只访问静态配置和不可变实体。</summary>
public sealed class BagRules
{
    /// <summary>基础列数。</summary>
    private readonly int _columns;
    /// <summary>基础行数。</summary>
    private readonly int _rows;
    /// <summary>按 ID 索引的独立只读道具配置。</summary>
    public IReadOnlyDictionary<int, ItemCfg> Items { get; }

    /// <summary>在组装边界接收配置快照，不依赖配置服务或文件。</summary>
    public BagRules(int columns, int rows, IEnumerable<ItemCfg> items)
    {
        _columns = columns;
        _rows = rows;
        GetCapacity(0);
        var definitions = items.ToDictionary(item => item.Id);
        if (definitions.Values.Any(item => item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name) || item.MaxStack <= 0))
            throw new InvalidDataException("服务端道具配置无效");
        Items = new ReadOnlyDictionary<int, ItemCfg>(definitions);
    }

    /// <summary>计算容量并拒绝扩容溢出。</summary>
    public int GetCapacity(int extraRows)
    {
        if (_columns <= 0 || _rows <= 0 || extraRows < 0) throw new InvalidDataException("背包容量参数无效");
        try { return checked(_columns * checked(_rows + extraRows)); }
        catch (OverflowException error) { throw new InvalidDataException("背包容量超出整数范围", error); }
    }

    /// <summary>先补堆叠再使用空格，构造失败不消耗实例编号。</summary>
    public ServerRoleCache Add(ServerRoleCache tables, int itemId, int count)
    {
        if (count <= 0 || !Items.TryGetValue(itemId, out var definition)) throw new InvalidDataException("道具或数量无效");
        Validate(tables);
        var capacity = GetCapacity(tables.Role.Value.BagExtraRows);
        var items = tables.Item.Items.Values.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex).ToList();
        var available = (long)(capacity - items.Count) * definition.MaxStack +
            items.Where(item => item.ItemId == itemId).Sum(item => (long)definition.MaxStack - item.Count);
        if (available < count) throw new InvalidOperationException("背包空间不足");
        var remaining = count;
        for (var index = 0; index < items.Count && remaining > 0; index++)
        {
            var item = items[index];
            if (item.ItemId != itemId) continue;
            var added = Math.Min(remaining, definition.MaxStack - item.Count);
            items[index] = item with { Count = item.Count + added };
            remaining -= added;
        }
        var occupied = items.Select(item => item.SlotIndex).ToHashSet();
        var nextId = tables.Role.Value.NextInstanceId;
        for (var slot = 0; remaining > 0 && slot < capacity; slot++)
        {
            if (occupied.Contains(slot)) continue;
            var added = Math.Min(remaining, definition.MaxStack);
            items.Add(new ItemEntity { InstanceId = nextId, ItemId = itemId, SlotIndex = slot, Count = added });
            nextId = checked(nextId + 1);
            remaining -= added;
        }
        var candidate = tables.WithItems(items.Concat(tables.Item.Items.Values.Where(item => item.Equipped)), nextId);
        Validate(candidate);
        return candidate;
    }

    /// <summary>直接替换物品穿戴字段，处理换装、卸下和双手冲突。</summary>
    public ServerRoleCache Equip(ServerRoleCache tables, long instanceId, EquipmentSlot slot, int? targetBagSlot = null)
    {
        Validate(tables);
        if (slot is not (EquipmentSlot.MainHand or EquipmentSlot.OffHand) || instanceId < 0)
            throw new InvalidOperationException("装备槽位或物品无效");
        var items = tables.Item.Items.Values.ToDictionary(item => item.InstanceId);
        var previous = items.Values.SingleOrDefault(item => item.EquipmentSlot == slot);
        if (instanceId > 0)
        {
            if (targetBagSlot.HasValue) throw new InvalidOperationException("穿戴不能指定卸下格子");
            if (!items.TryGetValue(instanceId, out var source) || source.Equipped) throw new InvalidOperationException("背包物品不存在");
            if (source.Count != 1 || !CanEquip(source.ItemId, slot)) throw new InvalidOperationException("道具不适配此装备槽位");
            items[source.InstanceId] = source with { Equipped = true, EquipmentSlot = slot, SlotIndex = 0 };
            if (previous is not null) items[previous.InstanceId] = previous with
                { Equipped = false, EquipmentSlot = null, SlotIndex = source.SlotIndex };
        }
        else
        {
            if (previous is null) throw new InvalidOperationException("此槽位没有装备");
            var capacity = GetCapacity(tables.Role.Value.BagExtraRows);
            var destination = targetBagSlot ?? FirstEmpty(items.Values, capacity);
            if (destination < 0 || destination >= capacity) throw new InvalidOperationException("背包空间不足或目标格无效");
            var target = items.Values.SingleOrDefault(item => !item.Equipped && item.SlotIndex == destination);
            if (target is not null)
            {
                if (target.Count != 1 || !CanEquip(target.ItemId, slot)) throw new InvalidOperationException("目标物品不适配此装备槽位");
                items[target.InstanceId] = target with { Equipped = true, EquipmentSlot = slot, SlotIndex = 0 };
            }
            items[previous.InstanceId] = previous with { Equipped = false, EquipmentSlot = null, SlotIndex = destination };
        }
        var main = items.Values.SingleOrDefault(item => item.EquipmentSlot == EquipmentSlot.MainHand);
        var off = items.Values.SingleOrDefault(item => item.EquipmentSlot == EquipmentSlot.OffHand);
        if (main is not null && off is not null && OccupiesBothHands(main.ItemId))
        {
            var conflict = slot == EquipmentSlot.MainHand ? off : main;
            var destination = FirstEmpty(items.Values, GetCapacity(tables.Role.Value.BagExtraRows));
            if (destination < 0) throw new InvalidOperationException("背包空间不足，无法收回冲突装备");
            items[conflict.InstanceId] = conflict with { Equipped = false, EquipmentSlot = null, SlotIndex = destination };
        }
        var candidate = tables.WithItems(items.Values);
        Validate(candidate);
        return candidate;
    }

    /// <summary>寻找首个未占用的背包格子。</summary>
    private static int FirstEmpty(IEnumerable<ItemEntity> items, int capacity)
    {
        var occupied = items.Where(item => !item.Equipped).Select(item => item.SlotIndex).ToHashSet();
        return Enumerable.Range(0, capacity).FirstOrDefault(index => !occupied.Contains(index), -1);
    }
    /// <summary>只依据配置分类判断槽位适配。</summary>
    private bool CanEquip(int itemId, EquipmentSlot slot) => Items.TryGetValue(itemId, out var definition) &&
        definition.MaxStack == 1 && EquipmentSlotUtil.CanEquip(definition.Category, definition.SubCategory, slot);
    /// <summary>按配置判断双手武器。</summary>
    private bool OccupiesBothHands(int itemId) => Items.TryGetValue(itemId, out var definition) &&
        EquipmentSlotUtil.OccupiesBothHands(definition.Category, definition.SubCategory);

    /// <summary>检查当前配置、容量、堆叠及双手组合，不修改实体。</summary>
    public void Validate(ServerRoleCache tables)
    {
        var capacity = GetCapacity(tables.Role.Value.BagExtraRows);
        foreach (var item in tables.Item.Items.Values)
        {
            if (!Items.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack ||
                (!item.Equipped ? item.SlotIndex >= capacity : !CanEquip(item.ItemId, item.EquipmentSlot!.Value)))
                throw new InvalidDataException("物品超出容量、配置不存在、数量超限或不适配槽位");
        }
        var main = tables.Item.Items.Values.SingleOrDefault(item => item.EquipmentSlot == EquipmentSlot.MainHand);
        if (main is not null && OccupiesBothHands(main.ItemId) && tables.Item.Items.Values.Any(item => item.EquipmentSlot == EquipmentSlot.OffHand))
            throw new InvalidDataException("双手武器不能同时装备副手物品");
    }
}
