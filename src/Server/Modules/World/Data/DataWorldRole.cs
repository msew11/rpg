using System.Collections.Immutable;
using Rpg.Protocol;

namespace Rpg.Server.Modules.World.Data;

/// <summary>地图持有的独立完整公开投影，不包含角色私有数据。</summary>
public sealed record DataWorldRole(long RoleId, string Name, int Level, int Gender, int HeadId,
    int Health, int MaxHealth, int Mana, int MaxMana, ImmutableArray<int> Equipment,
    int MapId, float X, float Y, float? TargetX, float? TargetY, float Speed, ulong Revision, ulong AcknowledgedAction)
{
    /// <summary>独占协议转为不可变值，不能在地图中留存 proto。</summary>
    internal static DataWorldRole FromPb(RoleWorldStatePb role, ulong revision, ulong acknowledged) => new(role.RoleId,
        role.Profile.Name, role.Profile.Level, (int)role.Profile.Gender, role.Profile.HeadId, role.Status.Health, role.Status.MaxHealth,
        role.Status.Mana, role.Status.MaxMana, role.Equipment.EquippedItems.ToImmutableArray(), role.Position.MapId, role.Position.X,
        role.Position.Y, role.Position.Target?.X, role.Position.Target?.Y, role.Position.Speed, revision, acknowledged);
    /// <summary>为每个接收方新建完整公开对象。</summary>
    internal RoleWorldStatePb ToPb() => new()
    {
        RoleId = RoleId, Profile = new() { Name = Name, Level = Level, Gender = (RoleGenderPb)Gender, HeadId = HeadId },
        Status = new() { Health = Health, MaxHealth = MaxHealth, Mana = Mana, MaxMana = MaxMana },
        Equipment = new() { EquippedItems = { Equipment } }, Position = Position()
    };
    /// <summary>新建完整位置组。</summary>
    internal RoleWorldPositionPb Position() => new() { MapId = MapId, X = X, Y = Y, Speed = Speed,
        Target = TargetX is { } x && TargetY is { } y ? new MoveTargetPb { X = x, Y = y } : null };
}

/// <summary>一个观察角色上次已发布的范围和可见版本。</summary>
internal sealed record DataWorldObserver(ulong Sequence, (int X, int Y) Center,
    ImmutableHashSet<(int X, int Y)> Chunks, ImmutableDictionary<long, DataWorldRole> Visible, ImmutableDictionary<System.Guid, DataWorldObject> Objects);
