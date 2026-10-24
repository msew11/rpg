using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

namespace Rpg.Server.Data;

/// <summary>独立原子保存的背包，下一个实例编号不会因失败而消耗。</summary>
public sealed record DataServerBag
{
    /// <summary>独立于角色移动状态的背包版本。</summary>
    public long Revision { get; init; }
    /// <summary>下次成功建立实例时使用的编号。</summary>
    public long NextInstanceId { get; init; } = 1;
    /// <summary>由本记录拥有的只读实例副本。</summary>
    private readonly IReadOnlyList<DataServerItem> _items = Array.AsReadOnly(Array.Empty<DataServerItem>());
    /// <summary>只记录已占用格子的实例列表，初始化时复制外部集合。</summary>
    public IReadOnlyList<DataServerItem> Items
    {
        get => _items;
        init => _items = value is null ? null! : Array.AsReadOnly(value.ToArray());
    }

    /// <summary>按背包内容比较记录，保证存档序列化前后角色值相等。</summary>
    public bool Equals(DataServerBag? other) => other is not null && Revision == other.Revision &&
        NextInstanceId == other.NextInstanceId && Items.SequenceEqual(other.Items);

    /// <summary>使用与内容相等规则一致的字段和实例哈希。</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Revision);
        hash.Add(NextInstanceId);
        foreach (var item in Items) hash.Add(item);
        return hash.ToHashCode();
    }

    /// <summary>校验与配置无关的存档完整性，避免重复编号或格子。</summary>
    public void Validate()
    {
        if (Revision < 0 || NextInstanceId <= 0 || Items is null ||
            Items.Any(item => item is null || item.InstanceId <= 0 || item.InstanceId >= NextInstanceId ||
                item.ItemId <= 0 || item.SlotIndex < 0 || item.Count <= 0) ||
            Items.Select(item => item.InstanceId).Distinct().Count() != Items.Count ||
            Items.Select(item => item.SlotIndex).Distinct().Count() != Items.Count)
            throw new InvalidDataException("背包存档包含无效编号、数量或重复格子");
    }
}
