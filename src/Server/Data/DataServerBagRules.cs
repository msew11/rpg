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

    /// <summary>构造穿脱候选，自动收回与目标槽冲突的另一手装备；失败不更改源状态。</summary>
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
        var main = equipment.SingleOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        var off = equipment.SingleOrDefault(item => item.Slot == EquipmentSlot.OffHand);
        if (main is not null && off is not null && OccupiesBothHands(main.ItemId))
        {
            var conflict = slot == EquipmentSlot.MainHand ? off : main;
            ReturnToBag(role, items, conflict);
            equipment.Remove(conflict);
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

    /// <summary>从服务端独立定义判断主手是否排斥副手。</summary>
    private bool OccupiesBothHands(int itemId) => Items.TryGetValue(itemId, out var definition) &&
        EquipmentSlotUtil.OccupiesBothHands(definition.Category, definition.SubCategory);

    /// <summary>将旧装备或冲突装备放入首个空格；候选空间不足时整次拒绝。</summary>
    private void ReturnToBag(DataServerRole role, List<DataServerItem> items, DataServerEquipment equipment)
    {
        var destination = Enumerable.Range(0, GetCapacity(role.BagExtraRows))
            .FirstOrDefault(index => items.All(item => item.SlotIndex != index), -1);
        if (destination < 0) throw new InvalidOperationException("背包空间不足，无法收回冲突装备");
        items.Add(new DataServerItem(equipment.InstanceId, equipment.ItemId, destination, 1));
    }

    /// <summary>选角前保留旧副手武器与冲突盾牌；不改原状态，无法回包则拒绝迁移。</summary>
    public DataServerRole MigrateLegacyEquipment(DataServerRole role)
    {
        Validate(role, true);
        var equipment = role.Bag.Equipment.ToList();
        var items = role.Bag.Items.ToList();
        var main = equipment.SingleOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        var off = equipment.SingleOrDefault(item => item.Slot == EquipmentSlot.OffHand);
        if (off is null || CanEquip(off.ItemId, EquipmentSlot.OffHand) &&
            (main is null || !OccupiesBothHands(main.ItemId))) return role;
        equipment.Remove(off);
        if (main is null && CanEquip(off.ItemId, EquipmentSlot.MainHand))
            equipment.Add(off with { Slot = EquipmentSlot.MainHand });
        else ReturnToBag(role, items, off);
        var candidate = role with { Bag = role.Bag with
        {
            Revision = checked(role.Bag.Revision + 1), EquipmentRevision = checked(role.Bag.EquipmentRevision + 1),
            Items = items.OrderBy(item => item.SlotIndex).ToArray(), Equipment = equipment.OrderBy(item => item.Slot).ToArray()
        } };
        Validate(candidate);
        return candidate;
    }

    /// <summary>在存档结构校验后核对当前配置与容量。</summary>
    public void Validate(DataServerRole role) => Validate(role, false);

    /// <summary>仅在迁移入口容许旧副手拳刃或暗器，其余非法数据始终拒绝。</summary>
    private void Validate(DataServerRole role, bool legacyHands)
    {
        role.Bag.Validate();
        var capacity = GetCapacity(role.BagExtraRows);
        foreach (var item in role.Bag.Items)
        {
            if (item.SlotIndex >= capacity || !Items.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack)
                throw new InvalidDataException("背包物品超出容量、配置不存在或数量超出堆叠上限");
        }
        foreach (var item in role.Bag.Equipment)
        {
            var legacy = legacyHands && item.Slot == EquipmentSlot.OffHand &&
                Items.TryGetValue(item.ItemId, out var definition) && definition.MaxStack == 1 &&
                EquipmentSlotUtil.HasPairedAppearance(definition.Category, definition.SubCategory);
            if (!CanEquip(item.ItemId, item.Slot) && !legacy) throw new InvalidDataException("装备配置不存在或不适配槽位");
        }
        var main = role.Bag.Equipment.SingleOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        if (!legacyHands && main is not null && OccupiesBothHands(main.ItemId) &&
            role.Bag.Equipment.Any(item => item.Slot == EquipmentSlot.OffHand))
            throw new InvalidDataException("双手武器不能同时装备副手物品");
    }
}
