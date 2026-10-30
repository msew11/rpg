using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>按 AOI 和权威距离查询地面物品。</summary>
internal sealed class GetGroundItemHandler : IActorHandler<WorldContext, GetGroundItem, GetGroundItemRt>
{
    /// <summary>只有当前 AOI 内实际距离不大于一瓦片的物品可被取回。</summary>
    public ValueTask<GetGroundItemRt> HandleAsync(WorldContext context, GetGroundItem request)
    {
        var origin = GroundItems.Position(context, request.Position);
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id)) throw new ArgumentException("地面对象编号无效");
        var item = context.Cache.Objects.Value.SingleOrDefault(item => item.ObjectId == id && item.MonsterTypeId is null)
            ?? throw new InvalidOperationException("地面物品已不存在");
        var layout = context.Cache.Layout; var target = new Vector2(item.X, item.Y);
        var center = context.Cache.Roles.Chunk(origin.X, origin.Y);
        if (!context.Cache.Roles.Neighborhood(center).Contains(context.Cache.Roles.Chunk(item.X, item.Y)) ||
            Vector2.DistanceSquared(origin, target) > (double)layout.TileSize * layout.TileSize)
            throw new InvalidOperationException("尚未到达拾取距离");
        return ValueTask.FromResult(new GetGroundItemRt { Object = GroundItems.ToPb(item) });
    }
}
