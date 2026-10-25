using System.IO;
using Godot;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>组合资料和位置的不可变角色快照，保留现有 UI 读取接口。</summary>
public sealed record RoleWorldStatePbData(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position, RoleGender Gender,
    long Revision, long AcknowledgedAction, int HeadId = 0, int BagExtraRows = 0, int MapId = 1)
{
    /// <summary>最近接纳的资料版本。</summary>
    public long ProfileRevision { get; init; } = Revision;
    /// <summary>最近接纳的位置版本。</summary>
    public long PositionRevision { get; init; } = Revision;

    /// <summary>校验并组合协议中的两部分状态。</summary>
    public static RoleWorldStatePbData FromPb(RoleWorldStatePb message)
    {
        if (message?.Profile is null || message.Position is null) throw new InvalidDataException("角色世界状态缺少资料或位置");
        return FromParts(RoleWorldProfilePbData.FromPb(message.Profile), RoleWorldPositionPbData.FromPb(message.Position));
    }

    /// <summary>把已验证的资料和位置组合为现有客户端快照。</summary>
    public static RoleWorldStatePbData FromParts(RoleWorldProfilePbData profile, RoleWorldPositionPbData position)
    {
        if (profile.RoleId != position.RoleId) throw new InvalidDataException("角色资料与位置身份不一致");
        return new RoleWorldStatePbData(profile.RoleId, profile.Name, profile.Level, profile.Health,
            profile.MaxHealth, profile.Mana, profile.MaxMana, profile.Experience, profile.NextExperience,
            position.Position, profile.Gender, System.Math.Max(profile.Revision, position.Revision),
            position.AcknowledgedAction, profile.HeadId, profile.BagExtraRows, position.MapId)
        { ProfileRevision = profile.Revision, PositionRevision = position.Revision };
    }

    /// <summary>创建完整角色协议快照。</summary>
    public RoleWorldStatePb ToPb() => new()
    {
        Profile = new RoleWorldProfilePbData(RoleId, Name, Level, Health, MaxHealth, Mana, MaxMana,
            Experience, NextExperience, Gender, HeadId, BagExtraRows, ProfileRevision).ToPb(),
        Position = new RoleWorldPositionPbData(RoleId, MapId, Position, PositionRevision, AcknowledgedAction).ToPb()
    };
}
