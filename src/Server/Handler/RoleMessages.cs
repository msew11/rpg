using System.Linq;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler;

/// <summary>从实体直接生成协议快照，不持有可变业务副本。</summary>
internal static class RoleMessages
{
    /// <summary>本人完整物品快照，装备槽位仅留在服务端。</summary>
    internal static BagPb Bag(ServerRoleCache tables)
    {
        var message = new BagPb();
        message.Items.AddRange(tables.Item.Items.Values.OrderBy(item => item.Equipped).ThenBy(item => item.SlotIndex)
            .ThenBy(item => item.EquipmentSlot).Select(item => new ItemPb
            {
                InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = item.SlotIndex,
                Count = item.Count, Equipped = item.Equipped,
                Equipment = tables.Equipment.Items.ContainsKey(item.InstanceId) ? new EquipmentPb() : null
            }));
        return message;
    }
    /// <summary>公开装备仅包含配置编号。</summary>
    internal static RoleWorldEquipmentPb Equipment(ServerRoleCache tables)
    {
        var message = new RoleWorldEquipmentPb { RoleId = tables.Role.Value.RoleId };
        message.EquippedItems.AddRange(tables.Item.Items.Values.Where(item => item.Equipped)
            .OrderBy(item => item.EquipmentSlot).Select(item => item.ItemId));
        return message;
    }
    /// <summary>本人一次接收同批物品与公开穿戴。</summary>
    internal static RoleItemsChangedNotice Items(ServerRoleCache tables) => new()
    { RoleId = tables.Role.Value.RoleId, Bag = Bag(tables), Equipment = Equipment(tables) };
    /// <summary>角色列表展示快照。</summary>
    internal static RoleSummaryPb Summary(RoleSummaryEntity role)
    {
        var message = new RoleSummaryPb { RoleId = role.RoleId, Name = role.Name, Level = role.Level,
            Gender = Gender(role.Gender), HeadId = role.HeadId };
        message.EquippedItems.AddRange(role.EquippedItems);
        return message;
    }
    /// <summary>转换身体性别。</summary>
    private static RoleGenderPb Gender(ServerRoleGender gender) => gender == ServerRoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female;
    /// <summary>公开角色资料。</summary>
    internal static RoleWorldProfilePb Profile(RoleEntity role) => new()
    { RoleId = role.RoleId, Name = role.Name, Level = role.Level, HeadId = role.HeadId, Gender = Gender(role.Gender) };
    /// <summary>可见生命和魔力。</summary>
    internal static RoleWorldStatusPb Status(RoleEntity role) => new()
    { RoleId = role.RoleId, Health = role.Health, MaxHealth = role.MaxHealth, Mana = role.Mana, MaxMana = role.MaxMana };
    /// <summary>私有经验及扩容。</summary>
    internal static RolePb Private(RoleEntity role) => new()
    { RoleId = role.RoleId, Experience = role.Experience, NextExperience = role.NextExperience, BagExtraRows = role.BagExtraRows };
    /// <summary>位置与当前会话确认序号。</summary>
    internal static RoleWorldPositionPb Position(RoleEntity role, long acknowledgedAction) => new()
    { RoleId = role.RoleId, MapId = role.MapId, X = role.X, Y = role.Y, AcknowledgedAction = acknowledgedAction };
    /// <summary>完整世界快照，不含私有角色字段。</summary>
    internal static RoleWorldStatePb World(ServerRoleCache tables, long acknowledgedAction) => new()
    { Profile = Profile(tables.Role.Value), Status = Status(tables.Role.Value), Position = Position(tables.Role.Value, acknowledgedAction), Equipment = Equipment(tables) };
}
