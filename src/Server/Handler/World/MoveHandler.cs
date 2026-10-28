using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.World;

/// <summary>接纳移动意图，最终位置由外层时间推进后确认。</summary>
public sealed class MoveHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public MoveRt Handle(ServerRequestContext cache, global::Rpg.Net.Message.Move request)
    {
        var response = new MoveRt { RoleId = request.RoleId, ActionId = request.ActionId };
        try
        {
            var role = cache.RequireRole(request.RoleId).Role.Value;
            var session = cache.Session;
            if (request.ActionId <= session.LastAction || !cache.MapLayouts[role.MapId].Contains(request.X, request.Y))
                throw new InvalidOperationException("无效的角色、坐标或移动序号");
            if (session.Pickup is { } canceled)
            {
                session.Pickup = null;
                cache.Notices.Enqueue(Bag.PickupOperations.Response(canceled, "拾取已取消"));
            }
            session.LastAction = request.ActionId;
            session.Target = new Vector2(request.X, request.Y);
            session.Moves.Add(new PendingMove(request.ActionId, 0.45));
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
