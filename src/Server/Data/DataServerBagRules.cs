using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

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
        var candidate = role with { Bag = new DataServerBag
        {
            Revision = checked(role.Bag.Revision + 1), NextInstanceId = nextId,
            Items = items.OrderBy(item => item.SlotIndex).ToList().AsReadOnly()
        } };
        Validate(candidate);
        return candidate;
    }

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
    }
}
