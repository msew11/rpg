using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Entity;

/// <summary>角色列表独立展示快照，不包含角色完整数据。</summary>
public sealed record RoleSummaryEntity
{
    /// <summary>角色身份。</summary>
    public long RoleId { get; init; }
    /// <summary>显示名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>角色等级。</summary>
    public int Level { get; init; }
    /// <summary>身体性别。</summary>
    public ServerRoleGender Gender { get; init; }
    /// <summary>头型。</summary>
    public int HeadId { get; init; } = 1;
    /// <summary>删除墓碑，保留编号防止复活。</summary>
    public bool IsDeleted { get; init; }
    /// <summary>由摘要拥有的装备编号列表。</summary>
    private readonly IReadOnlyList<int> _equippedItems = Array.Empty<int>();
    /// <summary>只供展示的 item.json 编号。</summary>
    public IReadOnlyList<int> EquippedItems
    {
        get => _equippedItems;
        init => _equippedItems = Array.AsReadOnly((value ?? throw new System.IO.InvalidDataException("角色摘要装备列表为空")).ToArray());
    }

    /// <summary>仅从已加载角色投影列表需要的字段。</summary>
    public static RoleSummaryEntity FromRole(ServerRoleCache tables) => new()
    {
        RoleId = tables.Role.Value.RoleId, Name = tables.Role.Value.Name, Level = tables.Role.Value.Level, Gender = tables.Role.Value.Gender,
        HeadId = tables.Role.Value.HeadId, IsDeleted = tables.Role.Value.IsDeleted,
        EquippedItems = tables.Item.Items.Values.Where(item => item.Equipped).OrderBy(item => item.EquipmentSlot).Select(item => item.ItemId).ToArray()
    };
}
