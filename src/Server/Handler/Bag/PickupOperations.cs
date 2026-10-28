using System;
using System.Numerics;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.Bag;

/// <summary>拾取 Handler 与时间推进共用的内存规则。</summary>
internal static class PickupOperations
{
    /// <summary>校验角色、版本、九块范围与最近移动目标。</summary>
    internal static MapObjectEntity Validate(RepoEntityCache cache, PickupItem request)
    {
        var tables = cache.RequireRole(request.RoleId);
        var session = cache.Session;
        if (request.MoveActionId <= 0 || request.MoveActionId != session.LastAction) throw new InvalidOperationException("拾取角色或移动意图无效");
        if (request.ExpectedBagRevision != session.ItemsRevision) throw new InvalidOperationException("背包已更新，请重新拾取");
        var map = cache.GetMap(tables.Role.Value.MapId);
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id) || !map.Objects.TryGetValue(id, out var item) || item.MonsterTypeId is not null)
            throw new InvalidOperationException("地面物品已不存在");
        if (request.ExpectedObjectRevision != item.Revision) throw new InvalidOperationException("地面物品已变化，请重新拾取");
        var center = map.Rules.ChunkAt(session.Position.X, session.Position.Y);
        if (!map.Rules.Neighborhood(center.X, center.Y).Contains(map.Rules.ChunkAt(item.X, item.Y)) ||
            Vector2.DistanceSquared(session.Target, new Vector2(item.X, item.Y)) > 0.01f)
            throw new InvalidOperationException("物品不在当前范围或走近目标不匹配");
        return item;
    }

    /// <summary>距离足够时整份入包；空值表示仍需等待，不触发 IO。</summary>
    internal static PickupItemRt? TryComplete(RepoEntityCache cache)
    {
        if (cache.Session.Pickup is not { } request) return null;
        try
        {
            var item = Validate(cache, request);
            var tables = cache.RequireRole(request.RoleId);
            var map = cache.GetMap(tables.Role.Value.MapId);
            if (Vector2.DistanceSquared(cache.Session.Position, new Vector2(item.X, item.Y)) > (double)map.Rules.TileSize * map.Rules.TileSize)
                return null;
            var candidate = cache.BagRules.Add(tables, item.ItemId, item.Count);
            var notice = RoleMessages.Items(candidate);
            var change = map.Remove(item.ObjectId);
            cache.Commit(candidate, 2);
            cache.TrackTransfer(tables.Role.Value.MapId, request.RoleId);
            cache.Session.Pickup = null;
            cache.Notices.Enqueue(notice);
            cache.PublishMapChange(map, change);
            return Response(request);
        }
        catch (Exception error)
        {
            cache.Session.Pickup = null;
            return Response(request, error.Message);
        }
    }

    /// <summary>最终响应仅包含关联身份，状态走独立通知。</summary>
    internal static PickupItemRt Response(PickupItem request, string error = "") => new()
    { RoleId = request.RoleId, ObjectId = request.ObjectId, MoveActionId = request.MoveActionId, Success = error.Length == 0, Error = error };
}
