using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>同一角色的平级实体缓存组，不复制字段或维护业务投影。</summary>
public sealed class RoleEntityTables : IEquatable<RoleEntityTables>
{
    /// <summary>角色单例缓存。</summary>
    public RoleEntityCache Role { get; }
    /// <summary>全部物品主键缓存。</summary>
    public ItemEntityCache Item { get; }
    /// <summary>装备扩展主键缓存。</summary>
    public EquipmentEntityCache Equipment { get; }
    /// <summary>未穿戴物品的格子有序视图，引用原实体。</summary>
    public IReadOnlyList<ItemEntity> BagItems => Item.Items.Values.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex).ToArray();
    /// <summary>已穿戴物品的槽位有序视图，引用原实体。</summary>
    public IReadOnlyList<ItemEntity> EquippedItems => Item.Items.Values.Where(item => item.Equipped).OrderBy(item => item.EquipmentSlot).ToArray();

    /// <summary>组合完整表并校验跨表编号、格子与穿戴槽唯一性。</summary>
    public RoleEntityTables(RoleEntityCache role, ItemEntityCache item, EquipmentEntityCache equipment)
    {
        Role = role;
        Item = item;
        Equipment = equipment;
        Validate();
    }

    /// <summary>检查各缓存共享的主键、编号、格子和穿戴槽位约束。</summary>
    private void Validate()
    {
        var value = Role.Value;
        RoleEntityCache.Validate(value);
        if (Item.Items.Values.Any(row => row.InstanceId >= value.NextInstanceId) ||
            Item.Items.Values.Where(row => !row.Equipped).Select(row => row.SlotIndex).Distinct().Count() != Item.Items.Values.Count(row => !row.Equipped) ||
            Item.Items.Values.Where(row => row.Equipped).Select(row => row.EquipmentSlot).Distinct().Count() != Item.Items.Values.Count(row => row.Equipped))
            throw new InvalidDataException("物品编号、背包格子或装备槽位重复或无效");
        _ = new EquipmentEntityCache(Equipment.Items.Values, Item);
    }

    /// <summary>从实体建立表组，未指定扩展时为物品创建初始扩展。</summary>
    public RoleEntityTables(RoleEntity role, IEnumerable<ItemEntity>? items = null, IEnumerable<EquipmentEntity>? equipment = null)
    {
        Role = new RoleEntityCache(role);
        Item = new ItemEntityCache(items ?? []);
        Equipment = new EquipmentEntityCache(equipment ?? Item.Items.Keys.Select(id => new EquipmentEntity { InstanceId = id }), Item);
        Validate();
    }

    /// <summary>仅替换角色单例，复用不可变物品表。</summary>
    public RoleEntityTables WithRole(RoleEntity value) => new(new RoleEntityCache(value), Item, Equipment);

    /// <summary>替换物品表并保留现有扩展数据，新增实例建立扩展。</summary>
    public RoleEntityTables WithItems(IEnumerable<ItemEntity> items, long? nextInstanceId = null)
    {
        var cache = new ItemEntityCache(items);
        var extensions = cache.Items.Keys.Select(id => Equipment.Items.TryGetValue(id, out var value)
            ? value : new EquipmentEntity { InstanceId = id });
        return new(new RoleEntityCache(Role.Value with { NextInstanceId = nextInstanceId ?? Role.Value.NextInstanceId }),
            cache, new EquipmentEntityCache(extensions, cache));
    }

    /// <summary>比较实体内容，忽略缓存对象身份。</summary>
    public bool Equals(RoleEntityTables? other) => other is not null && Role.Value == other.Role.Value &&
        Item.Items.OrderBy(row => row.Key).SequenceEqual(other.Item.Items.OrderBy(row => row.Key)) &&
        Equipment.Items.OrderBy(row => row.Key).SequenceEqual(other.Equipment.Items.OrderBy(row => row.Key));
    /// <summary>按实体字段判断表组相等。</summary>
    public override bool Equals(object? obj) => obj is RoleEntityTables other && Equals(other);
    /// <summary>实体组的哈希由角色主键决定。</summary>
    public override int GetHashCode() => Role.Value.RoleId.GetHashCode();
}
