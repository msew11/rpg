using System.IO;
using System.Collections.Generic;
using Rpg.Config.Data;
using Godot;
using Rpg.Const;
using Rpg.Protocol;

namespace Rpg.Session.Data;

/// <summary>本地组合可见世界和私有角色数据的不可变读取快照。</summary>
public sealed record RoleWorldStatePbData(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position, RoleGender Gender,
    long Revision, long AcknowledgedAction, int HeadId = 0, int BagExtraRows = 0, int MapId = 1)
{
    /// <summary>权威移动目标。</summary>
    public Vector2? Target { get; init; }
    /// <summary>当前服务器速度。</summary>
    public float Speed { get; init; }
    /// <summary>独立公开且只读的当前装备。</summary>
    public RoleWorldEquipmentPbData Equipment { get; init; } = RoleWorldEquipmentPbData.Empty(RoleId);
    /// <summary>最近接纳的资料版本。</summary>
    public long ProfileRevision { get; init; } = Revision;
    /// <summary>最近接纳的状态版本。</summary>
    public long StatusRevision { get; init; } = Revision;
    /// <summary>最近接纳的位置版本。</summary>
    public long PositionRevision { get; init; } = Revision;
    /// <summary>最近接纳的私有角色版本。</summary>
    public long RoleRevision { get; init; } = Revision;

    /// <summary>组合根身份和公开、私有字段。</summary>
    public static RoleWorldStatePbData FromPb(global::Rpg.Protocol.RoleWorldStatePb message, global::Rpg.Protocol.RolePb role, IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message?.Profile is null || message.Status is null || message.Position is null || message.Equipment is null || message.RoleId <= 0)
            throw new InvalidDataException("角色世界状态不完整");
        return FromParts(RoleWorldProfilePbData.FromPb(message.Profile, message.RoleId),
            RoleWorldStatusPbData.FromPb(message.Status, message.RoleId), RoleWorldPositionPbData.FromPb(message.Position, message.RoleId),
            RolePbData.FromPb(role, message.RoleId), RoleWorldEquipmentPbData.FromPb(message.Equipment, message.RoleId, definitions));
    }

    /// <summary>把五份已校验数据合成仅供当前玩家读取的快照。</summary>
    public static RoleWorldStatePbData FromParts(RoleWorldProfilePbData profile, RoleWorldStatusPbData status,
        RoleWorldPositionPbData position, RolePbData role, RoleWorldEquipmentPbData equipment)
    {
        if (profile.RoleId != status.RoleId || profile.RoleId != position.RoleId || profile.RoleId != role.RoleId || profile.RoleId != equipment.RoleId)
            throw new InvalidDataException("角色领域身份不一致");
        return new RoleWorldStatePbData(profile.RoleId, profile.Name, profile.Level, status.Health,
            status.MaxHealth, status.Mana, status.MaxMana, role.Experience, role.NextExperience,
            position.Position, profile.Gender,
            System.Math.Max(System.Math.Max(profile.Revision, status.Revision),
                System.Math.Max(position.Revision, role.Revision)),
            position.AcknowledgedAction, profile.HeadId, role.BagExtraRows, position.MapId)
        { Target = position.Target, Speed = position.Speed, ProfileRevision = profile.Revision, StatusRevision = status.Revision,
            PositionRevision = position.Revision, RoleRevision = role.Revision, Equipment = equipment };
    }

    /// <summary>仅序列化可见世界状态，绝不包含经验和背包容量。</summary>
    public RoleWorldStatePb ToPb() => new()
    {
        RoleId = RoleId,
        Profile = new RoleWorldProfilePbData(RoleId, Name, Level, Gender, HeadId, ProfileRevision).ToPb(),
        Status = new RoleWorldStatusPbData(RoleId, Health, MaxHealth, Mana, MaxMana, StatusRevision).ToPb(),
        Position = new RoleWorldPositionPbData(RoleId, MapId, Position, PositionRevision, AcknowledgedAction) { Target = Target, Speed = Speed }.ToPb(),
        Equipment = Equipment.ToPb()
    };

    /// <summary>单独序列化当前玩家私有领域。</summary>
    public RolePb ToRolePb() => new RolePbData(RoleId, Experience, NextExperience,
        BagExtraRows, RoleRevision).ToPb();
}
