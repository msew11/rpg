using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.Bag;

/// <summary>完整校验后销毁或转移道具，登记地图共同落地。</summary>
public sealed class DropItemHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public DropItemRt Handle(RepoEntityCache cache, global::Rpg.Net.Message.DropItem request)
    {
        var response = new DropItemRt { RoleId = request.RoleId, InstanceId = request.InstanceId };
        try
        {
            var tables = cache.RequireRole(request.RoleId);
            if (request.ExpectedRevision != cache.Session.ItemsRevision) throw new InvalidOperationException("背包已更新，请重新选择丢弃物品");
            cache.BagRules.Validate(tables);
            if (!tables.Item.Items.TryGetValue(request.InstanceId, out var source) || source.Equipped) throw new ArgumentException("源物品不存在");
            if (request.Count <= 0 || request.Count > source.Count) throw new ArgumentException("丢弃数量无效");
            var items = tables.Item.Items.Values.Where(item => item.InstanceId != source.InstanceId)
                .Concat(request.Count == source.Count ? [] : new[] { source with { Count = source.Count - request.Count } });
            var candidate = tables.WithItems(items);
            cache.BagRules.Validate(candidate);
            var notice = RoleMessages.Items(candidate);
            if (!request.Destroy)
            {
                var map = cache.GetMap(tables.Role.Value.MapId);
                if (request.HasDropX != request.HasDropY) throw new ArgumentException("丢弃落点坐标不完整");
                var point = request.HasDropX ? GroundDrop.Validate(request, map.Rules, cache.Session.Position) : GroundDrop.Choose(map.Rules, cache.Session.Position);
                var ground = new MapObjectEntity { X = point.X, Y = point.Y, ItemId = source.ItemId, Count = request.Count };
                // 地图创建包含完整校验；后续内存提交使用已验证的实体候选。
                var change = map.Create(ground);
                cache.Commit(candidate, 2);
                cache.TrackTransfer(tables.Role.Value.MapId, request.RoleId);
                cache.Notices.Enqueue(notice);
                cache.PublishMapChange(map, change);
                response.ObjectId = ground.ObjectId.ToString("N");
            }
            else
            {
                cache.Commit(candidate, 2);
                cache.Notices.Enqueue(notice);
            }
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
