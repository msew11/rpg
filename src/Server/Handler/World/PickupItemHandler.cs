using System;
using Rpg.Net.Message;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.World;

/// <summary>接纳走近拾取意图，近处立即提交，远处由模拟推进继续。</summary>
public sealed class PickupItemHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public PickupItemRt Handle(WorldContext cache, PickupItem request)
    {
        try
        {
            _ = PickupOperations.Validate(cache, request);
            if (cache.Role.Session.Pickup is { } pending)
            {
                if (pending.Equals(request)) return PickupOperations.Response(request);
                throw new InvalidOperationException("已有待完成的拾取");
            }
            cache.Role.Session.Pickup = request.Clone();
            return PickupOperations.TryComplete(cache) ?? PickupOperations.Response(request);
        }
        catch (Exception error) { return PickupOperations.Response(request, error.Message); }
    }
}
