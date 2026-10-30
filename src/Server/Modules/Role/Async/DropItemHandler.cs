using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>验证角色候选并向地图转移丢弃物品。</summary>
internal sealed class DropItemHandler : IActorHandler<RoleContext, DropItem, DropItemRt>
{
    /// <summary>丢弃先建立完整候选，地图创建成功后提交；销毁只更新持有数量。</summary>
    public async ValueTask<DropItemRt> HandleAsync(RoleContext context, DropItem request)
    {
        var cache = context.Cache;
        cache.Movement.Advance(cache.Role, context.Call!.ServerTimeMs, context.Call.MovementSpeed);
        var tables = cache.Snapshot(); var candidate = RoleItemRules.Create(context).Remove(tables, request.InstanceId, request.Count);
        var source = tables.Items.Single(item => item.InstanceId == request.InstanceId);
        var reply = new DropItemRt { RoleId = cache.RoleId, InstanceId = request.InstanceId };
        if (!request.Destroy)
        {
            var create = new CreateGroundItem { ItemId = source.ItemId, Count = request.Count, Position = RoleProjection.Build(context).Position };
            if (request.HasDropX) create.DropX = request.DropX;
            if (request.HasDropY) create.DropY = request.DropY;
            var result = await context.Actors.World(tables.Role.MapId).Ask<CreateGroundItemRt>(create, context.Call).ConfigureAwait(false);
            reply.ObjectId = result.Object.ObjectId;
        }
        cache.Commit(candidate); return reply;
    }
}
