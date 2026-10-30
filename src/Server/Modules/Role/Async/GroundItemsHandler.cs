using System;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>在已有物品串行入口内直接向地图转移，先验证角色候选。</summary>
internal sealed class GroundItemsHandler : IActorHandler<RoleContext, DropItem, DropItemRt>,
    IActorHandler<RoleContext, PickupItem, PickupItemRt>, IActorHandler<RoleContext, CompleteRolePickup, CompleteRolePickupRt>
{
    /// <summary>丢弃先建立完整候选，地图创建成功后提交；销毁只更新持有数量。</summary>
    public async ValueTask<DropItemRt> HandleAsync(RoleContext context, DropItem request)
    {
        var cache = context.Cache;
        cache.Movement.Advance(cache.Role, context.Call!.ServerTimeMs, context.Call.MovementSpeed);
        var tables = cache.Snapshot(); var candidate = RoleItemsHandler.Rules(context).Remove(tables, request.InstanceId, request.Count);
        var source = tables.Items.Single(item => item.InstanceId == request.InstanceId);
        var reply = new DropItemRt { RoleId = cache.RoleId, InstanceId = request.InstanceId };
        if (!request.Destroy)
        {
            var create = new CreateGroundItem { ItemId = source.ItemId, Count = request.Count, Position = Handler.RoleProjection.Build(context).Position };
            if (request.HasDropX) create.DropX = request.DropX;
            if (request.HasDropY) create.DropY = request.DropY;
            var result = await context.Actors.World(tables.Role.MapId).Ask<CreateGroundItemRt>(create, context.Call).ConfigureAwait(false);
            reply.ObjectId = result.Object.ObjectId;
        }
        cache.Commit(candidate); return reply;
    }
    /// <summary>只接纳当前移动意图，立即回复，最终容量在完成回合验证。</summary>
    public ValueTask<PickupItemRt> HandleAsync(RoleContext context, PickupItem request)
    {
        var cache = context.Cache;
        if (!Guid.TryParseExact(request.ObjectId, "N", out _) || request.MoveActionId == 0 || request.MoveActionId != cache.Movement.Value.AcknowledgedAction)
            throw new ArgumentException("拾取对象或移动意图无效");
        cache.Pickup = new(request.ObjectId, request.MoveActionId);
        return ValueTask.FromResult(new PickupItemRt { RoleId = cache.RoleId, ObjectId = request.ObjectId, MoveActionId = request.MoveActionId });
    }
    /// <summary>在当前目标一瓦片内安排实际地图校验；目标已到达同样可完成。</summary>
    internal static bool Ready(RoleContext context)
    {
        var cache = context.Cache;
        if (cache.Pickup is null) return false;
        var move = cache.Movement.Value;
        if (move.TargetX is not { } x || move.TargetY is not { } y) return true;
        var role = cache.Role.Value; var dx = role.X - x; var dy = role.Y - y;
        var tile = context.Config.Map.Items[role.MapId].TileSize;
        return (double)dx * dx + (double)dy * dy <= (double)tile * tile;
    }
    /// <summary>地图查询、容量候选和删除在共享串行入口内完成，明确取消没有结果。</summary>
    public async ValueTask<CompleteRolePickupRt> HandleAsync(RoleContext context, CompleteRolePickup request)
    {
        var cache = context.Cache;
        if (cache.Pickup is not { } pending || !Ready(context)) return new();
        cache.Pickup = null;
        var result = new PickupItemResultNotice { RoleId = cache.RoleId, ObjectId = pending.ObjectId, MoveActionId = pending.MoveActionId };
        try
        {
            var world = context.Actors.World(cache.Role.Value.MapId);
            var found = await world.Ask<GetGroundItemRt>(new GetGroundItem { ObjectId = pending.ObjectId, Position = Handler.RoleProjection.Build(context).Position }, context.Call).ConfigureAwait(false);
            var item = found.Object.GroundItem;
            var candidate = RoleItemsHandler.Rules(context).Add(cache.Snapshot(), item.ItemId, item.Count);
            await world.Ask<RemoveGroundItemRt>(new RemoveGroundItem { ObjectId = pending.ObjectId }, context.Call).ConfigureAwait(false);
            cache.Commit(candidate); result.Success = true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.InvalidDataException) { /* 最终失败只携带 success。 */ }
        return new() { Result = result };
    }
}
