using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>在共享物品入口内完成角色与地图的拾取转移。</summary>
internal sealed class CompleteRolePickupHandler : IActorHandler<RoleContext, CompleteRolePickup, CompleteRolePickupRt>
{
    /// <summary>地图查询、容量候选和删除在共享串行入口内完成，明确取消没有结果。</summary>
    public async ValueTask<CompleteRolePickupRt> HandleAsync(RoleContext context, CompleteRolePickup request)
    {
        var cache = context.Cache;
        if (cache.Pickup is not { } pending || !PickupIntent.Ready(context)) return new();
        cache.Pickup = null;
        var result = new PickupItemResultNotice { RoleId = cache.RoleId, ObjectId = pending.ObjectId, MoveActionId = pending.MoveActionId };
        try
        {
            var world = context.Actors.World(cache.Role.Value.MapId);
            var found = await world.Ask<GetGroundItemRt>(new GetGroundItem { ObjectId = pending.ObjectId, Position = RoleProjection.Build(context).Position }, context.Call).ConfigureAwait(false);
            var item = found.Object.GroundItem;
            var candidate = RoleItemRules.Create(context).Add(cache.Snapshot(), item.ItemId, item.Count);
            await world.Ask<RemoveGroundItemRt>(new RemoveGroundItem { ObjectId = pending.ObjectId }, context.Call).ConfigureAwait(false);
            cache.Commit(candidate); result.Success = true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.InvalidDataException) { /* 最终失败只携带 success。 */ }
        return new() { Result = result };
    }
}
