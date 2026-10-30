using System.IO;
using System.Collections.Generic;
using Godot;
using Rpg.Config.Data;

namespace Rpg.Session.Data;

/// <summary>其他玩家的独立公开快照，不包含私有角色和背包数据。</summary>
public sealed record DataPublicRole(long RoleId, RoleWorldProfilePbData Profile, RoleWorldStatusPbData Status,
    RoleWorldPositionPbData Position, RoleWorldEquipmentPbData Equipment)
{
    /// <summary>全量公开信息一次验证并复制。</summary>
    public static DataPublicRole FromPb(global::Rpg.Protocol.RoleWorldStatePb value, IReadOnlyDictionary<int, ItemCfg> items)
    {
        if (value is null || value.RoleId <= 0) throw new InvalidDataException("公开角色身份无效");
        return new(value.RoleId, RoleWorldProfilePbData.FromPb(value.Profile, value.RoleId),
            RoleWorldStatusPbData.FromPb(value.Status, value.RoleId), RoleWorldPositionPbData.FromPb(value.Position, value.RoleId),
            RoleWorldEquipmentPbData.FromPb(value.Equipment, value.RoleId, items));
    }
}
