using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>在当前地图创建地面物品。</summary>
internal sealed class CreateGroundItemHandler : IActorHandler<WorldContext, CreateGroundItem, CreateGroundItemRt>
{
    /// <summary>合法候选落点确认后一次加入地图。</summary>
    public ValueTask<CreateGroundItemRt> HandleAsync(WorldContext context, CreateGroundItem request)
    {
        var origin = GroundItems.Position(context, request.Position);
        if (!context.Config.Item.Items.TryGetValue(request.ItemId, out var cfg) || request.Count <= 0 || request.Count > cfg.MaxStack || request.HasDropX != request.HasDropY)
            throw new ArgumentException("地面物品数量或落点无效");
        var point = request.HasDropX ? GroundDrop.Validate(request, context.Cache.Layout, origin) : GroundDrop.Choose(context.Cache.Layout, origin);
        var item = new DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 1, X = point.X, Y = point.Y, ItemId = request.ItemId, Count = request.Count };
        context.Cache.Objects.Commit(context.Cache.Objects.Value.Add(item)); GroundItems.Mark(context, item);
        return ValueTask.FromResult(new CreateGroundItemRt { Object = GroundItems.ToPb(item) });
    }
}
