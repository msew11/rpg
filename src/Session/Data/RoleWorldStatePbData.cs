using System.IO;
using Godot;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>本地组合可见世界和私有角色数据的不可变读取快照。</summary>
public sealed record RoleWorldStatePbData(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position, RoleGender Gender,
    long Revision, long AcknowledgedAction, int HeadId = 0, int BagExtraRows = 0, int MapId = 1)
{
    /// <summary>最近接纳的资料版本。</summary>
    public long ProfileRevision { get; init; } = Revision;
    /// <summary>最近接纳的状态版本。</summary>
    public long StatusRevision { get; init; } = Revision;
    /// <summary>最近接纳的位置版本。</summary>
    public long PositionRevision { get; init; } = Revision;
    /// <summary>最近接纳的私有角色版本。</summary>
    public long RoleRevision { get; init; } = Revision;

    /// <summary>校验并组合世界状态与独立返回的私有角色领域。</summary>
    public static RoleWorldStatePbData FromPb(RoleWorldStatePb message, RolePb role)
    {
        if (message?.Profile is null || message.Status is null || message.Position is null)
            throw new InvalidDataException("角色世界状态缺少资料、状态或位置");
        return FromParts(RoleWorldProfilePbData.FromPb(message.Profile),
            RoleWorldStatusPbData.FromPb(message.Status), RoleWorldPositionPbData.FromPb(message.Position),
            RolePbData.FromPb(role));
    }

    /// <summary>把四份已校验数据合成仅供当前玩家读取的快照。</summary>
    public static RoleWorldStatePbData FromParts(RoleWorldProfilePbData profile, RoleWorldStatusPbData status,
        RoleWorldPositionPbData position, RolePbData role)
    {
        if (profile.RoleId != status.RoleId || profile.RoleId != position.RoleId || profile.RoleId != role.RoleId)
            throw new InvalidDataException("角色领域身份不一致");
        return new RoleWorldStatePbData(profile.RoleId, profile.Name, profile.Level, status.Health,
            status.MaxHealth, status.Mana, status.MaxMana, role.Experience, role.NextExperience,
            position.Position, profile.Gender,
            System.Math.Max(System.Math.Max(profile.Revision, status.Revision),
                System.Math.Max(position.Revision, role.Revision)),
            position.AcknowledgedAction, profile.HeadId, role.BagExtraRows, position.MapId)
        { ProfileRevision = profile.Revision, StatusRevision = status.Revision,
            PositionRevision = position.Revision, RoleRevision = role.Revision };
    }

    /// <summary>仅序列化可见世界状态，绝不包含经验和背包容量。</summary>
    public RoleWorldStatePb ToPb() => new()
    {
        Profile = new RoleWorldProfilePbData(RoleId, Name, Level, Gender, HeadId, ProfileRevision).ToPb(),
        Status = new RoleWorldStatusPbData(RoleId, Health, MaxHealth, Mana, MaxMana, StatusRevision).ToPb(),
        Position = new RoleWorldPositionPbData(RoleId, MapId, Position, PositionRevision, AcknowledgedAction).ToPb()
    };

    /// <summary>单独序列化当前玩家私有领域。</summary>
    public RolePb ToRolePb() => new RolePbData(RoleId, Experience, NextExperience,
        BagExtraRows, RoleRevision).ToPb();
}
