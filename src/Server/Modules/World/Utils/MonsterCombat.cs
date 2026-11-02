using System;
using System.Linq;
using Rpg.Protocol;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.ActorProtocol;

namespace Rpg.Server.Modules.World.Utils;

/// <summary>地图邮箱内的怪物距离检查和即时事件捕获。</summary>
internal static class MonsterCombat
{
    /// <summary>使用可信Role位置复查怪物、范围及所在地图。</summary>
    internal static DataWorldObject? Target(WorldContext context, string id, RoleWorldPositionPb position, double range)
    {
        if (position is null || position.MapId != context.Cache.MapId || !double.IsFinite(range) || range <= 0 ||
            !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !Guid.TryParseExact(id, "N", out var guid)) return null;
        var target = context.Cache.Objects.Value.FirstOrDefault(item => item.ObjectId == guid && item.MonsterTypeId is not null && item.Health > 0);
        if (target is null || !context.Cache.Roles.Neighborhood(context.Cache.Roles.Chunk(position.X, position.Y))
            .Contains(context.Cache.Roles.Chunk(target.X, target.Y))) return null;
        var dx = (double)target.X - position.X; var dy = (double)target.Y - position.Y;
        return dx * dx + dy * dy <= range * range + 0.001 ? target : null;
    }
    /// <summary>在地图邮箱内固定本次事件的AOI收件人。</summary>
    internal static void Queue(WorldContext context, CombatEventPb value)
    {
        float x, y;
        if (value.Started is { } started) { x = started.TargetX; y = started.TargetY; }
        else if (value.Hit is { } hit) { x = hit.Target.X; y = hit.Target.Y; }
        else if (value.Cancelled is { } cancelled && context.Cache.Roles.Value.TryGetValue(cancelled.RoleId, out var role)) { x = role.X; y = role.Y; }
        else return;
        var delivery = new CombatDeliveryPb { Event = value.Clone() };
        var observers = context.Cache.Roles.Observers(x, y).ToHashSet();
        var attackerId = value.Started?.RoleId ?? value.Hit?.RoleId ?? value.Cancelled!.RoleId;
        if (context.Cache.Roles.Value.TryGetValue(attackerId, out var attacker))
            observers.UnionWith(context.Cache.Roles.Observers(attacker.X, attacker.Y));
        delivery.ObserverRoleIds.AddRange(observers);
        context.Cache.CombatEvents.Enqueue(delivery);
    }
}
