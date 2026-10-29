using System;
using Rpg.ServerV2.Modules.Role.Data;

namespace Rpg.ServerV2.Modules.Role.Cache;

/// <summary>角色邮箱独占的三表工作区，不执行 IO。</summary>
public sealed class RoleCache
{
    /// <summary>固定角色身份，不使用连接编号。</summary>
    public long RoleId { get; }
    /// <summary>角色基础表。</summary>
    public DataCacheRole Role { get; }
    /// <summary>物品实例表。</summary>
    public DataCacheItem Items { get; }
    /// <summary>装备扩展表。</summary>
    public DataCacheEquipment Equipments { get; }
    /// <summary>直接汇总各表缓存的待写状态。</summary>
    public DataRoleTables DirtyTables => (Role.IsDirty ? DataRoleTables.Role : DataRoleTables.None)
        | (Items.IsDirty ? DataRoleTables.Item : DataRoleTables.None)
        | (Equipments.IsDirty ? DataRoleTables.Equipment : DataRoleTables.None);
    /// <summary>用完整加载的三表建立工作区和保存基线。</summary>
    internal RoleCache(long roleId, DataRoleSnapshot snapshot)
    { RoleId = roleId; Role = new(snapshot.Role); Items = new(snapshot.Items); Equipments = new(snapshot.Equipments); }
    /// <summary>导出不可变三表，后续提交不修改导出值。</summary>
    public DataRoleSnapshot Snapshot() => new(Role.Value, Items.Value, Equipments.Value);
    /// <summary>回合内一次接纳三表，先检查结构再修改缓存。</summary>
    public void Commit(DataRoleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Role);
        Role.Commit(snapshot.Role); Items.Commit(snapshot.Items); Equipments.Commit(snapshot.Equipments);
    }
    /// <summary>完整保存成功后共同更新三表基线。</summary>
    internal void AcceptSaved() { Role.AcceptSaved(); Items.AcceptSaved(); Equipments.AcceptSaved(); }
}
