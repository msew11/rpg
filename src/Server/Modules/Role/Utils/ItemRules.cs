using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Server.Modules.Role.Data;
using System.Collections.Immutable;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;

using Rpg.Utils;

namespace Rpg.Server.Modules.Role.Utils;

/// <summary>处理器共用的背包规则，只访问静态配置和不可变实体。</summary>
internal sealed class ItemRules
{
    /// <summary>基础列数。</summary>
    private readonly int _columns;
    /// <summary>基础行数。</summary>
    private readonly int _rows;
    /// <summary>按 ID 索引的独立只读道具配置。</summary>
    public IReadOnlyDictionary<int, ItemCfg> Items { get; }

    /// <summary>在组装边界接收配置快照，不依赖配置服务或文件。</summary>
    internal ItemRules(int columns, int rows, IEnumerable<ItemCfg> items)
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
    public DataRoleSnapshot Add(DataRoleSnapshot tables, int itemId, int count)
    {
        if (count <= 0 || !Items.TryGetValue(itemId, out var definition)) throw new InvalidDataException("道具或数量无效");
        Validate(tables);
        var capacity = GetCapacity(tables.Role.BagExtraRows);
        var items = tables.Items.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex).ToList();
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
        var nextId = tables.Role.NextInstanceId;
        for (var slot = 0; remaining > 0 && slot < capacity; slot++)
        {
            if (occupied.Contains(slot)) continue;
            var added = Math.Min(remaining, definition.MaxStack);
            items.Add(new DataItem { InstanceId = nextId, ItemId = itemId, SlotIndex = slot, Count = added });
            nextId = checked(nextId + 1);
            remaining -= added;
        }
        var candidate = WithItems(tables, items.Concat(tables.Items.Where(item => item.Equipped)), nextId);
        Validate(candidate);
        return candidate;
    }

    /// <summary>直接替换物品穿戴字段，处理换装、卸下和双手冲突。</summary>
    public DataRoleSnapshot Equip(DataRoleSnapshot tables, long instanceId, EquipmentSlot slot, int? targetBagSlot = null)
    {
        Validate(tables);
        if (slot is not (EquipmentSlot.MainHand or EquipmentSlot.OffHand) || instanceId < 0)
            throw new InvalidOperationException("装备槽位或物品无效");
        var items = tables.Items.ToDictionary(item => item.InstanceId);
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
            var capacity = GetCapacity(tables.Role.BagExtraRows);
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
            var destination = FirstEmpty(items.Values, GetCapacity(tables.Role.BagExtraRows));
            if (destination < 0) throw new InvalidOperationException("背包空间不足，无法收回冲突装备");
            items[conflict.InstanceId] = conflict with { Equipped = false, EquipmentSlot = null, SlotIndex = destination };
        }
        var candidate = WithItems(tables, items.Values);
        Validate(candidate);
        return candidate;
    }

    /// <summary>寻找首个未占用的背包格子。</summary>
    private static int FirstEmpty(IEnumerable<DataItem> items, int capacity)
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

    /// <summary>验证完整候选，拒绝重复实例、非法格子及不完整扩展。</summary>
    public void Validate(DataRoleSnapshot tables)
    {
        var capacity = GetCapacity(tables.Role.BagExtraRows);
        if (tables.Role.NextInstanceId <= 0 || tables.Items.Any(item => item is null || item.InstanceId <= 0 || item.InstanceId >= tables.Role.NextInstanceId || item.Count <= 0 || item.SlotIndex < 0 ||
            !Items.TryGetValue(item.ItemId, out var cfg) || item.Count > cfg.MaxStack ||
            (item.Equipped ? item.SlotIndex != 0 || item.Count != 1 || item.EquipmentSlot is not { } slot || !CanEquip(item.ItemId, slot) : item.EquipmentSlot.HasValue || item.SlotIndex >= capacity)) ||
            tables.Items.Select(item => item.InstanceId).Distinct().Count() != tables.Items.Length ||
            tables.Items.Where(item => !item.Equipped).Select(item => item.SlotIndex).Distinct().Count() != tables.Items.Count(item => !item.Equipped) ||
            tables.Items.Where(item => item.Equipped).Select(item => item.EquipmentSlot).Distinct().Count() != tables.Items.Count(item => item.Equipped))
            throw new InvalidDataException("物品实例、数量或槽位无效");
        var expected = tables.Items.Where(item => EquipmentSlotUtil.TryGetSlot(Items[item.ItemId].Category, Items[item.ItemId].SubCategory, out _)).Select(item => item.InstanceId).Order();
        if (!expected.SequenceEqual(tables.Equipments.Select(item => item.InstanceId).Order())) throw new InvalidDataException("物品与装备扩展不一致");
        var main = tables.Items.SingleOrDefault(item => item.EquipmentSlot == EquipmentSlot.MainHand);
        if (main is not null && OccupiesBothHands(main.ItemId) && tables.Items.Any(item => item.EquipmentSlot == EquipmentSlot.OffHand))
            throw new InvalidDataException("双手武器不能同时装备副手");
    }
    /// <summary>由物品候选同步空装备扩展，并保留实例编号。</summary>
    private DataRoleSnapshot WithItems(DataRoleSnapshot tables, IEnumerable<DataItem> values, long? nextId = null)
    {
        var items = values.OrderBy(item => item.InstanceId).ToImmutableArray();
        return new(tables.Role with { NextInstanceId = nextId ?? tables.Role.NextInstanceId }, items,
            items.Where(item => EquipmentSlotUtil.TryGetSlot(Items[item.ItemId].Category, Items[item.ItemId].SubCategory, out _)).Select(item => new DataEquipment { InstanceId = item.InstanceId }).ToImmutableArray());
    }
    /// <summary>只减少背包内持有数量，销毁与丢弃共用候选校验。</summary>
    internal DataRoleSnapshot Remove(DataRoleSnapshot tables, long instanceId, int count)
    {
        Validate(tables);
        var source = tables.Items.SingleOrDefault(item => item.InstanceId == instanceId && !item.Equipped);
        if (source is null || count <= 0 || count > source.Count) throw new ArgumentException("持有物品或数量无效");
        var items = tables.Items.Where(item => item.InstanceId != instanceId);
        if (count < source.Count) items = items.Append(source with { Count = source.Count - count });
        var candidate = WithItems(tables, items); Validate(candidate); return candidate;
    }
    /// <summary>按最新目标格移动、堆叠或交换。</summary>
    public DataRoleSnapshot Move(DataRoleSnapshot tables, long instanceId, int destination)
    {
        Validate(tables);
        if (destination < 0 || destination >= GetCapacity(tables.Role.BagExtraRows)) throw new ArgumentException("目标格子超出范围");
        var source = tables.Items.SingleOrDefault(item => item.InstanceId == instanceId && !item.Equipped) ?? throw new ArgumentException("源物品不存在");
        if (source.SlotIndex == destination) return tables;
        var target = tables.Items.SingleOrDefault(item => !item.Equipped && item.SlotIndex == destination);
        var items = tables.Items.ToDictionary(item => item.InstanceId); items.Remove(instanceId);
        if (target is null) items[instanceId] = source with { SlotIndex = destination };
        else if (target.ItemId == source.ItemId && target.Count < Items[source.ItemId].MaxStack)
        {
            var amount = Math.Min(source.Count, Items[source.ItemId].MaxStack - target.Count);
            items[target.InstanceId] = target with { Count = target.Count + amount };
            if (source.Count > amount) items[instanceId] = source with { Count = source.Count - amount };
        }
        else { items[instanceId] = source with { SlotIndex = destination }; items[target.InstanceId] = target with { SlotIndex = source.SlotIndex }; }
        var candidate = WithItems(tables, items.Values); Validate(candidate); return candidate;
    }
    /// <summary>输出不带版本的本人完整物品快照。</summary>
    public GetRoleItemsRt Snapshot(DataRoleSnapshot tables)
    {
        Validate(tables);
        var result = new GetRoleItemsRt { RoleId = tables.Role.RoleId, Bag = new(), Equipment = new() };
        foreach (var item in tables.Items)
        {
            var value = new ItemPb { InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = item.SlotIndex, Count = item.Count, Equipped = item.Equipped };
            if (tables.Equipments.Any(ext => ext.InstanceId == item.InstanceId)) value.Equipment = new EquipmentPb();
            result.Bag.Items.Add(value);
            if (item.Equipped) result.Equipment.EquippedItems.Add(item.ItemId);
        }
        return result;
    }
}
