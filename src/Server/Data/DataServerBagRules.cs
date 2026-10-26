using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Rpg.Const;
using Rpg.Utils;

namespace Rpg.Server.Data;

/// <summary>服务端独立且只读的背包配置，构造时复制全部规则。</summary>
public sealed class DataServerBagRules
{
    /// <summary>基础列数。</summary>
    private readonly int _columns;
    /// <summary>基础行数。</summary>
    private readonly int _rows;
    /// <summary>按配置编号索引的独立定义。</summary>
    public IReadOnlyDictionary<int, DataServerItemDefinition> Items { get; }

    /// <summary>复制外部定义并在启动边界验证基础规则。</summary>
    public DataServerBagRules(int columns, int rows, IEnumerable<DataServerItemDefinition> items)
    {
        _columns = columns;
        _rows = rows;
        GetCapacity(0);
        var definitions = items.ToDictionary(item => item.Id, item => item with { });
        if (definitions.Values.Any(item => item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name) || item.MaxStack <= 0))
            throw new InvalidDataException("服务端道具配置无效");
        Items = new ReadOnlyDictionary<int, DataServerItemDefinition>(definitions);
    }

    /// <summary>服务端独立计算容量并拒绝扩容溢出。</summary>
    public int GetCapacity(int extraRows)
    {
        if (_columns <= 0 || _rows <= 0 || extraRows < 0) throw new InvalidDataException("背包容量参数无效");
        try { return checked(_columns * checked(_rows + extraRows)); }
        catch (OverflowException error) { throw new InvalidDataException("背包容量超出整数范围", error); }
    }

    /// <summary>原子构造完整入包候选，先补堆叠再占空格，不修改源状态或消耗失败编号。</summary>
    public DataServerRole Add(DataServerRole role, int itemId, int count)
    {
        if (count <= 0 || !Items.TryGetValue(itemId, out var definition)) throw new InvalidDataException("道具或数量无效");
        Validate(role);
        var capacity = GetCapacity(role.BagExtraRows);
        var available = (long)(capacity - role.Bag.Items.Count) * definition.MaxStack;
        available += role.Bag.Items.Where(item => item.ItemId == itemId).Sum(item => (long)definition.MaxStack - item.Count);
        if (available < count) throw new InvalidOperationException("背包空间不足");
        var items = role.Bag.Items.OrderBy(item => item.SlotIndex).ToList();
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
        var nextId = role.Bag.NextInstanceId;
        for (var slot = 0; remaining > 0 && slot < capacity; slot++)
        {
            if (occupied.Contains(slot)) continue;
            var added = Math.Min(remaining, definition.MaxStack);
            items.Add(new DataServerItem(nextId, itemId, slot, added));
            nextId = checked(nextId + 1);
            remaining -= added;
        }
        var candidate = role with { Bag = role.Bag with
        {
            Revision = checked(role.Bag.Revision + 1), NextInstanceId = nextId,
            Items = items.OrderBy(item => item.SlotIndex).ToList().AsReadOnly()
        } };
        Validate(candidate);
        return candidate;
    }

    /// <summary>构造穿戴、替换或卸下候选，保持实例与编号序列，失败不更改源状态。</summary>
    public DataServerRole Equip(DataServerRole role, long instanceId, EquipmentSlot slot, int? targetBagSlot = null)
    {
        Validate(role);
        if (slot is not (EquipmentSlot.MainHand or EquipmentSlot.OffHand) || instanceId < 0)
            throw new InvalidOperationException("装备槽位或物品无效");
        var items = role.Bag.Items.ToList();
        var equipment = role.Bag.Equipment.ToList();
        var previous = equipment.SingleOrDefault(item => item.Slot == slot);
        if (instanceId > 0)
        {
            if (targetBagSlot.HasValue) throw new InvalidOperationException("穿戴不能指定卸下格子");
            var source = items.SingleOrDefault(item => item.InstanceId == instanceId)
                ?? throw new InvalidOperationException("背包物品不存在");
            if (source.Count != 1 || !CanEquip(source.ItemId, slot)) throw new InvalidOperationException("道具不适配此装备槽位");
            items.Remove(source);
            if (previous is not null)
            {
                equipment.Remove(previous);
                items.Add(new DataServerItem(previous.InstanceId, previous.ItemId, source.SlotIndex, 1));
            }
            equipment.Add(new DataServerEquipment(source.InstanceId, source.ItemId, slot));
        }
        else
        {
            if (previous is null) throw new InvalidOperationException("此槽位没有装备");
            var capacity = GetCapacity(role.BagExtraRows);
            var destination = targetBagSlot ?? Enumerable.Range(0, capacity).FirstOrDefault(index => items.All(item => item.SlotIndex != index), -1);
            if (destination < 0 || destination >= capacity) throw new InvalidOperationException("背包空间不足或目标格无效");
            var target = items.SingleOrDefault(item => item.SlotIndex == destination);
            equipment.Remove(previous);
            if (target is not null)
            {
                if (target.Count != 1 || !CanEquip(target.ItemId, slot)) throw new InvalidOperationException("目标物品不适配此装备槽位");
                items.Remove(target);
                equipment.Add(new DataServerEquipment(target.InstanceId, target.ItemId, slot));
            }
            items.Add(new DataServerItem(previous.InstanceId, previous.ItemId, destination, 1));
        }
        var candidate = role with { Bag = role.Bag with { Revision = checked(role.Bag.Revision + 1),
            EquipmentRevision = checked(role.Bag.EquipmentRevision + 1),
            Items = items.OrderBy(item => item.SlotIndex).ToArray(), Equipment = equipment.OrderBy(item => item.Slot).ToArray() } };
        Validate(candidate);
        return candidate;
    }

    /// <summary>只按服务端启动时复制的静态分类校验目标槽位。</summary>
    private bool CanEquip(int itemId, EquipmentSlot slot) => Items.TryGetValue(itemId, out var definition) &&
        definition.MaxStack == 1 && EquipmentSlotUtil.CanEquip(definition.Category, definition.SubCategory, slot);

    /// <summary>在存档结构校验后核对当前配置与容量。</summary>
    public void Validate(DataServerRole role)
    {
        role.Bag.Validate();
        var capacity = GetCapacity(role.BagExtraRows);
        foreach (var item in role.Bag.Items)
        {
            if (item.SlotIndex >= capacity || !Items.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack)
                throw new InvalidDataException("背包物品超出容量、配置不存在或数量超出堆叠上限");
        }
        foreach (var item in role.Bag.Equipment)
            if (!CanEquip(item.ItemId, item.Slot)) throw new InvalidDataException("装备配置不存在或不适配槽位");
    }
}
