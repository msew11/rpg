using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Handler;

/// <summary>在本地角色缓存接纳拾取意图。</summary>
internal sealed class PickupItemHandler : IActorHandler<RoleContext, PickupItem, PickupItemRt>
{
    /// <summary>只接纳当前移动意图，立即回复，最终容量在完成回合验证。</summary>
    public ValueTask<PickupItemRt> HandleAsync(RoleContext context, PickupItem request)
    {
        var cache = context.Cache;
        if (!Guid.TryParseExact(request.ObjectId, "N", out _) || request.MoveActionId == 0 || request.MoveActionId != cache.Movement.Value.AcknowledgedAction)
            throw new ArgumentException("拾取对象或移动意图无效");
        cache.Pickup = new(request.ObjectId, request.MoveActionId);
        return ValueTask.FromResult(new PickupItemRt { RoleId = cache.RoleId, ObjectId = request.ObjectId, MoveActionId = request.MoveActionId });
    }
}
