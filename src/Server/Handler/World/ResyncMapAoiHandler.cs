using System;
using Rpg.Net.Message;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.World;

/// <summary>只返回当前角色的权威 AOI 范围。</summary>
public sealed class ResyncMapAoiHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public ResyncMapAoiRt Handle(WorldContext cache, ResyncMapAoi request)
    {
        var response = new ResyncMapAoiRt { RoleId = request.RoleId, MapId = request.MapId };
        try
        {
            var role = cache.Role.RequireRole(request.RoleId).Role.Value;
            if (cache.Role.Session.Aoi is null || request.MapId != role.MapId) throw new InvalidOperationException("重同步角色或地图身份无效");
            response.Snapshot = cache.Role.Session.Aoi.Snapshot(cache.GetMap(role.MapId), cache.Role.Session.Position.X, cache.Role.Session.Position.Y);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
