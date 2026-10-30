using System.Linq;
using Rpg.ProtocolV2;

namespace Rpg.ServerV2.Modules.Role.Handler;

/// <summary>只投影公开资料和完整位置组，不携带私有玩法数据。</summary>
internal static class RoleProjection
{
    /// <summary>由当前角色缓存生成独占协议对象。</summary>
    internal static RoleWorldStatePb Build(RoleContext context)
    {
        var cache = context.Cache; var role = cache.Role.Value; var movement = cache.Movement.Value;
        return new RoleWorldStatePb
        {
            RoleId = role.RoleId,
            Profile = new() { Name = role.Name, Level = role.Level, Gender = (RoleGenderPb)role.Gender, HeadId = role.HeadId },
            Status = new() { Health = role.Health, MaxHealth = role.MaxHealth, Mana = role.Mana, MaxMana = role.MaxMana },
            Equipment = new() { EquippedItems = { cache.Items.Value.Where(item => item.Equipped).Select(item => item.ItemId) } },
            Position = new() { MapId = role.MapId, X = role.X, Y = role.Y, Speed = movement.Speed,
                Target = movement.TargetX is { } x && movement.TargetY is { } y ? new MoveTargetPb { X = x, Y = y } : null }
        };
    }
}
