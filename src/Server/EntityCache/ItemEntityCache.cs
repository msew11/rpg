using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>以 InstanceId 为主键的角色物品多例缓存。</summary>
public sealed class ItemEntityCache
{
    /// <summary>只读主键索引，内部对象均不可变。</summary>
    public IReadOnlyDictionary<long, ItemEntity> Items { get; }

    /// <summary>完整检查后建立索引，不接受重复主键。</summary>
    public ItemEntityCache(IEnumerable<ItemEntity> values)
    {
        var items = new Dictionary<long, ItemEntity>();
        foreach (var item in values)
        {
            if (item is null || item.InstanceId <= 0 || item.ItemId <= 0 || item.Count <= 0 || item.SlotIndex < 0 ||
                item.Equipped && (item.SlotIndex != 0 || item.Count != 1 || item.EquipmentSlot is null || !Enum.IsDefined(item.EquipmentSlot.Value)) ||
                !item.Equipped && item.EquipmentSlot is not null || !items.TryAdd(item.InstanceId, item))
                throw new InvalidDataException("物品实体包含非法字段或重复 InstanceId");
        }
        Items = new ReadOnlyDictionary<long, ItemEntity>(items);
    }
}
