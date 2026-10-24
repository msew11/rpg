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
