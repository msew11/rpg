using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>以物品 InstanceId 关联装备扩展的多例缓存。</summary>
public sealed class EquipmentEntityCache
{
    /// <summary>装备扩展主键索引。</summary>
    public IReadOnlyDictionary<long, EquipmentEntity> Items { get; }

    /// <summary>拒绝无效、重复或找不到对应物品的扩展行。</summary>
    public EquipmentEntityCache(IEnumerable<EquipmentEntity> values, ItemEntityCache itemCache)
    {
        var items = new Dictionary<long, EquipmentEntity>();
        foreach (var item in values)
            if (item is null || item.InstanceId <= 0 || !itemCache.Items.ContainsKey(item.InstanceId) ||
                !items.TryAdd(item.InstanceId, item))
                throw new InvalidDataException("装备实体包含非法关联或重复 InstanceId");
        foreach (var item in itemCache.Items.Values)
            if (item.Equipped && !items.ContainsKey(item.InstanceId))
                throw new InvalidDataException("已穿物品缺少装备扩展实体");
        Items = new ReadOnlyDictionary<long, EquipmentEntity>(items);
    }
}
