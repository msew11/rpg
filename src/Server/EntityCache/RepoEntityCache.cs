using System;
using System.Linq;
using Rpg.Server.Data;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>仓库实体组，平级持有各表缓存并投影完整业务候选。</summary>
public sealed class RepoEntityCache
{
    /// <summary>角色单例表缓存。</summary>
    public RoleEntityCache Role { get; }
    /// <summary>物品多例表缓存。</summary>
    public ItemEntityCache Item { get; }
    /// <summary>装备扩展多例表缓存。</summary>
    public EquipmentEntityCache Equipment { get; }
    /// <summary>仅业务模拟使用的临时角色序号，不序列化。</summary>
    private readonly long _roleRevision;
    /// <summary>仅业务候选使用的临时物品序号，不序列化。</summary>
    private readonly long _bagRevision;
    /// <summary>仅业务候选使用的临时装备序号，不序列化。</summary>
    private readonly long _equipmentRevision;
    /// <summary>当前移动确认序号，不作为实体表落地。</summary>
    private readonly long _acknowledgedAction;

    /// <summary>组合独立加载的表缓存，并校验完整业务候选。</summary>
    public RepoEntityCache(RoleEntityCache role, ItemEntityCache item, EquipmentEntityCache equipment)
    {
        Role = role;
        Item = item;
        Equipment = equipment;
        RepoServerRole.Validate(ToRole());
    }

    /// <summary>将业务候选拆成平级表缓存，不保留聚合副本。</summary>
    public RepoEntityCache(DataServerRole role)
    {
        RepoServerRole.Validate(role);
        Role = new RoleEntityCache(new RoleEntity
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
        });
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

    /// <summary>从三个表缓存投影既有业务规则所需的不可变候选。</summary>
    public DataServerRole ToRole() => new()
    {
        RoleId = Role.Value.RoleId,
        MapId = Role.Value.MapId,
        Name = Role.Value.Name,
        Level = Role.Value.Level,
        Health = Role.Value.Health,
        MaxHealth = Role.Value.MaxHealth,
        Mana = Role.Value.Mana,
        MaxMana = Role.Value.MaxMana,
        Experience = Role.Value.Experience,
        NextExperience = Role.Value.NextExperience,
        X = Role.Value.X,
        Y = Role.Value.Y,
        Gender = Role.Value.Gender,
        HeadId = Role.Value.HeadId,
        IsDeleted = Role.Value.IsDeleted,
        BagExtraRows = Role.Value.BagExtraRows,
        Revision = _roleRevision, AcknowledgedAction = _acknowledgedAction,
        Bag = new DataServerBag
        {
            Revision = _bagRevision, EquipmentRevision = _equipmentRevision,
            NextInstanceId = Role.Value.NextInstanceId,
            Items = Item.Items.Values.Where(item => !item.Equipped).OrderBy(item => item.SlotIndex)
                .Select(item => new DataServerItem(item.InstanceId, item.ItemId, item.SlotIndex, item.Count)).ToArray(),
            Equipment = Item.Items.Values.Where(item => item.Equipped).OrderBy(item => item.EquipmentSlot)
                .Select(item => new DataServerEquipment(item.InstanceId, item.ItemId, item.EquipmentSlot!.Value)).ToArray()
        }
    };
}
