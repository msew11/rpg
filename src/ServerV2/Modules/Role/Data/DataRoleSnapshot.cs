using System.Collections.Immutable;
using System;

namespace Rpg.ServerV2.Modules.Role.Data;

/// <summary>角色可独立写回的三张表。</summary>
[Flags]
public enum DataRoleTables
{
    /// <summary>没有待写内容。</summary>
    None = 0,
    /// <summary>角色基础数据。</summary>
    Role = 1,
    /// <summary>物品实例表。</summary>
    Item = 2,
    /// <summary>装备扩展表。</summary>
    Equipment = 4,
    /// <summary>完整角色存档。</summary>
    All = Role | Item | Equipment
}

/// <summary>仓库与所属 Actor 之间交换的不可变角色三表快照。</summary>
/// <param name="Role">角色单例。</param>
/// <param name="Items">物品实例。</param>
/// <param name="Equipments">装备扩展。</param>
public sealed record DataRoleSnapshot(DataRole Role, ImmutableArray<DataItem> Items,
    ImmutableArray<DataEquipment> Equipments);
