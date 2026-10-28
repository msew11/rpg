using System;
using System.Linq;
using Rpg.Server.Data;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>一个角色的单例实体及其物品、装备扩展缓存。</summary>
public sealed class RoleEntityCache
{
    /// <summary>role.json 对应的单个角色实体。</summary>
    public RoleEntity Value { get; }
    /// <summary>当前角色物品多例表。</summary>
    public ItemEntityCache Item { get; }
    /// <summary>当前角色装备扩展多例表。</summary>
    public EquipmentEntityCache Equipment { get; }
    /// <summary>仅业务模拟使用的临时角色序号，不序列化。</summary>
    private readonly long _roleRevision;
    /// <summary>仅业务候选使用的临时物品序号，不序列化。</summary>
    private readonly long _bagRevision;
    /// <summary>仅业务候选使用的临时装备序号，不序列化。</summary>
    private readonly long _equipmentRevision;
    /// <summary>当前移动确认序号，不作为实体表落地。</summary>
    private readonly long _acknowledgedAction;

    /// <summary>由完整实体表建立缓存，网络版本从零开始。</summary>
    public RoleEntityCache(RoleEntity value, ItemEntityCache item, EquipmentEntityCache equipment)
    {
        Value = value;
        Item = item;
        Equipment = equipment;
        RepoServerRole.Validate(ToRole());
    }

    /// <summary>将业务候选拆成实际缓存的单例及多例实体，不保留聚合副本。</summary>
    public RoleEntityCache(DataServerRole role)
    {
        RepoServerRole.Validate(role);
        Value = new RoleEntity
        {
            RoleId = role.RoleId,
            MapId = role.MapId,
            Name = role.Name,
            Level = role.Level,
            Health = role.Health,
            MaxHealth = role.MaxHealth,
            Mana = role.Mana,
            MaxMana = role.MaxMana,
            Experience = role.Experience,
            NextExperience = role.NextExperience,
            X = role.X,
            Y = role.Y,
            Gender = role.Gender,
            HeadId = role.HeadId,
            IsDeleted = role.IsDeleted,
            BagExtraRows = role.BagExtraRows,
            NextInstanceId = role.Bag.NextInstanceId
        };
        Item = new ItemEntityCache(role.Bag.Items.Select(item => new ItemEntity
        {
            InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = item.SlotIndex, Count = item.Count
        }).Concat(role.Bag.Equipment.Select(item => new ItemEntity
        {
            InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = 0, Count = 1,
            Equipped = true, EquipmentSlot = item.Slot
        })));
        Equipment = new EquipmentEntityCache(Item.Items.Values.Select(item =>
            new EquipmentEntity { InstanceId = item.InstanceId }), Item);
        _roleRevision = role.Revision;
        _bagRevision = role.Bag.Revision;
        _equipmentRevision = role.Bag.EquipmentRevision;
        _acknowledgedAction = role.AcknowledgedAction;
    }

    /// <summary>从实体缓存投影现有业务规则所需的不可变候选。</summary>
    public DataServerRole ToRole() => new()
    {
            RoleId = Value.RoleId,
            MapId = Value.MapId,
            Name = Value.Name,
            Level = Value.Level,
            Health = Value.Health,
            MaxHealth = Value.MaxHealth,
            Mana = Value.Mana,
            MaxMana = Value.MaxMana,
            Experience = Value.Experience,
            NextExperience = Value.NextExperience,
            X = Value.X,
            Y = Value.Y,
            Gender = Value.Gender,
            HeadId = Value.HeadId,
            IsDeleted = Value.IsDeleted,
            BagExtraRows = Value.BagExtraRows,
            Revision = _roleRevision, AcknowledgedAction = _acknowledgedAction,
            Bag = new DataServerBag
            {
                Revision = _bagRevision, EquipmentRevision = _equipmentRevision,
                NextInstanceId = Value.NextInstanceId,
                Items = Item.Items.Values.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex)
                    .Select(item => new DataServerItem(item.InstanceId, item.ItemId, item.SlotIndex, item.Count)).ToArray(),
                Equipment = Item.Items.Values.Where(item => item.Equipped).OrderBy(item => item.EquipmentSlot)
                    .Select(item => new DataServerEquipment(item.InstanceId, item.ItemId, item.EquipmentSlot!.Value)).ToArray()
            }
    };
}
