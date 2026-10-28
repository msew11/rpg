using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>单个角色 Actor 独占的三张实体表与瞬态模拟。</summary>
public sealed class ServerRoleCache : IEquatable<ServerRoleCache>
{
    /// <summary>本角色的单例实体缓存。</summary>
    public RoleEntityCache Role { get; private set; }
    /// <summary>包含未穿与已穿实例的物品缓存。</summary>
    public ItemEntityCache Item { get; private set; }
    /// <summary>按物品实例编号关联的装备扩展缓存。</summary>
    public EquipmentEntityCache Equipment { get; private set; }
    /// <summary>未穿戴物品的格子有序视图，引用原实体。</summary>
    public IReadOnlyList<ItemEntity> BagItems => Item.Items.Values.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex).ToArray();
    /// <summary>已穿戴物品的槽位有序视图，引用原实体。</summary>
    public IReadOnlyList<ItemEntity> EquippedItems => Item.Items.Values.Where(item => item.Equipped).OrderBy(item => item.EquipmentSlot).ToArray();
    /// <summary>待写领域：角色 1、物品 2、共同变更 3。</summary>
    internal int Dirty { get; set; }
    /// <summary>本角色的模拟、订阅和消息版本。</summary>
    public ServerSession Session { get; internal set; } = new();

    /// <summary>接收完整实体缓存并校验跨表约束。</summary>
    public ServerRoleCache(RoleEntityCache role, ItemEntityCache item, EquipmentEntityCache equipment)
    {
        Role = role;
        Item = item;
        Equipment = equipment;
        Validate();
    }

    /// <summary>从实体建立缓存，未指定扩展时为物品创建初始扩展。</summary>
    public ServerRoleCache(RoleEntity role, IEnumerable<ItemEntity>? items = null, IEnumerable<EquipmentEntity>? equipment = null)
    {
        Role = new RoleEntityCache(role);
        Item = new ItemEntityCache(items ?? []);
        Equipment = new EquipmentEntityCache(equipment ?? Item.Items.Keys.Select(id => new EquipmentEntity { InstanceId = id }), Item);
        Validate();
    }

    /// <summary>共享已校验的不可变实体缓存，独立复制待写标记与会话。</summary>
    private ServerRoleCache(ServerRoleCache source)
    {
        Role = source.Role;
        Item = source.Item;
        Equipment = source.Equipment;
        Dirty = source.Dirty;
        Session = source.Session.Copy();
    }

    /// <summary>检查编号、背包格子、装备槽位及扩展关联。</summary>
    private void Validate()
    {
        RoleEntityCache.Validate(Role.Value);
        if (Item.Items.Values.Any(row => row.InstanceId >= Role.Value.NextInstanceId) ||
            Item.Items.Values.Where(row => !row.Equipped).Select(row => row.SlotIndex).Distinct().Count() != Item.Items.Values.Count(row => !row.Equipped) ||
            Item.Items.Values.Where(row => row.Equipped).Select(row => row.EquipmentSlot).Distinct().Count() != Item.Items.Values.Count(row => row.Equipped))
            throw new InvalidDataException("物品编号、背包格子或装备槽位重复或无效");
        _ = new EquipmentEntityCache(Equipment.Items.Values, Item);
    }

    /// <summary>构造角色实体候选，会话和待写状态由提交方保留。</summary>
    public ServerRoleCache WithRole(RoleEntity value) => new(new RoleEntityCache(value), Item, Equipment);

    /// <summary>构造物品候选，保留现有扩展并同步新增、删除关联。</summary>
    public ServerRoleCache WithItems(IEnumerable<ItemEntity> items, long? nextInstanceId = null)
    {
        var cache = new ItemEntityCache(items);
        var extensions = cache.Items.Keys.Select(id => Equipment.Items.TryGetValue(id, out var value)
            ? value : new EquipmentEntity { InstanceId = id });
        return new(new RoleEntityCache(Role.Value with { NextInstanceId = nextInstanceId ?? Role.Value.NextInstanceId }),
            cache, new EquipmentEntityCache(extensions, cache));
    }

    /// <summary>接纳已校验候选的全部实体，不替换当前会话或待写状态。</summary>
    internal void ReplaceEntities(ServerRoleCache candidate)
    {
        Role = candidate.Role;
        Item = candidate.Item;
        Equipment = candidate.Equipment;
    }

    /// <summary>建立可以安全移交给地图邮箱的工作副本。</summary>
    internal ServerRoleCache Copy() => new(this);

    /// <summary>只比较持久化实体内容，忽略会话、待写标记与缓存身份。</summary>
    public bool Equals(ServerRoleCache? other) => other is not null && Role.Value == other.Role.Value &&
        Item.Items.OrderBy(row => row.Key).SequenceEqual(other.Item.Items.OrderBy(row => row.Key)) &&
        Equipment.Items.OrderBy(row => row.Key).SequenceEqual(other.Equipment.Items.OrderBy(row => row.Key));
    /// <summary>按持久化实体字段判断缓存内容相等。</summary>
    public override bool Equals(object? obj) => obj is ServerRoleCache other && Equals(other);
    /// <summary>实体缓存的哈希由角色主键决定。</summary>
    public override int GetHashCode() => Role.Value.RoleId.GetHashCode();
}
