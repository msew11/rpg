using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.Context;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.World;

/// <summary>从已加载表选择当前角色并准备首次 AOI。</summary>
public sealed class SelectRoleHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public SelectRoleRt Handle(WorldContext cache, global::Rpg.Net.Message.SelectRole request)
    {
        var response = new SelectRoleRt { RoleId = request.RoleId };
        try
        {
            if (!cache.Role.Session.Authenticated) throw new InvalidOperationException("请先登录");
            var tables = cache.Role.GetRole(request.RoleId);
            cache.Role.Rules.BagRules.Validate(tables);
            var role = tables.Role.Value;
            var map = cache.GetMap(role.MapId);
            if (!map.Rules.Contains(role.X, role.Y)) throw new InvalidOperationException("角色位置超出所属地图");
            var observer = new MapAoiTracker(role.RoleId, role.MapId);
            var snapshot = observer.Snapshot(map, role.X, role.Y);
            response.State = RoleMessages.World(tables, 0);
            response.Role = RoleMessages.Private(role);
            response.Bag = RoleMessages.Bag(tables);
            cache.Role.Session.Reset();
            cache.Role.Session.RoleId = role.RoleId;
            cache.Role.Session.Position = cache.Role.Session.Target = new Vector2(role.X, role.Y);
            cache.Role.Session.Aoi = observer;
            response.Success = true;
            cache.Role.Notices.Enqueue(new MapAoiNotice { RoleId = role.RoleId, MapId = role.MapId, Sequence = snapshot.Sequence, Snapshot = snapshot });
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
