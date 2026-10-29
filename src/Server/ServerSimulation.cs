using System;
using System.Numerics;
using Rpg.Net.Message;
using Rpg.Server.Context;
using Rpg.Server.Handler;
using Rpg.Server.Handler.World;

namespace Rpg.Server;

/// <summary>推进会话模拟和延迟响应，只提交内存，不接触文件与传输。</summary>
internal static class ServerSimulation
{
    /// <summary>按环境速度推进移动，再完成可达拾取和 AOI 差集。</summary>
    internal static void Advance(WorldContext cache, double delta, int movementSpeed)
    {
        AdvanceMovement(cache.Role, delta, movementSpeed);
        var pickup = PickupOperations.TryComplete(cache);
        if (pickup is not null) cache.Role.Notices.Enqueue(pickup);
        if (cache.Role.Current is { } current && cache.Role.Session.Aoi is { } aoi)
        {
            var notice = aoi.Move(cache.GetMap(current.Role.Value.MapId), cache.Role.Session.Position.X, cache.Role.Session.Position.Y);
            if (notice is not null) cache.Role.Notices.Enqueue(notice);
        }
    }

    /// <summary>维持 0.45 秒确认和三像素修正，失败终止相关待确认意图。</summary>
    private static void AdvanceMovement(RoleContext cache, double delta, int speed)
    {
        if (cache.Current is null) return;
        var session = cache.Session;
        var distance = Vector2.Distance(session.Position, session.Target);
        var step = (float)(speed * delta);
        session.Position = distance <= step || distance == 0 ? session.Target : session.Position + (session.Target - session.Position) / distance * step;
        for (var index = 0; index < session.Moves.Count;)
        {
            var pending = session.Moves[index] with { Remaining = session.Moves[index].Remaining - delta };
            if (pending.Remaining > 0) { session.Moves[index++] = pending; continue; }
            var current = cache.Current!;
            var role = current.Role.Value;
            try
            {
                var rules = cache.Rules.MapLayouts[role.MapId];
                var candidate = current.WithRole(role with
                { X = Math.Clamp(session.Position.X + 3, 0, (long)rules.Width * rules.TileSize - 1), Y = session.Position.Y });
                cache.Commit(candidate, 1);
                session.AcknowledgedAction = pending.ActionId;
                session.Moves.RemoveAt(index);
                cache.Notices.Enqueue(new MoveRt { Success = true, RoleId = role.RoleId, ActionId = pending.ActionId,
                    Position = RoleMessages.Position(candidate.Role.Value, pending.ActionId) });
            }
            catch (Exception error)
            {
                session.Position = session.Target = new Vector2(role.X, role.Y);
                var failed = session.Moves.ToArray();
                session.Moves.Clear();
                session.Pickup = null;
                foreach (var move in failed) cache.Notices.Enqueue(new MoveRt { RoleId = role.RoleId, ActionId = move.ActionId,
                    Error = $"角色缓存提交失败：{error.Message}" });
                return;
            }
        }
    }
}
