using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Rpg.ProtocolV2;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.World.Handler;

/// <summary>地图独占地面对象，直接创建查询删除，不反向调用角色。</summary>
internal sealed class GroundItemsHandler : IActorHandler<WorldContext, CreateGroundItem, CreateGroundItemRt>,
    IActorHandler<WorldContext, GetGroundItem, GetGroundItemRt>, IActorHandler<WorldContext, RemoveGroundItem, RemoveGroundItemRt>
{
    /// <summary>检查来源权威位置属于当前地图。</summary>
    private static Vector2 Position(WorldContext context, RoleWorldPositionPb position)
    {
        if (position is null || position.MapId != context.Cache.MapId || !GroundDrop.Contains(context.Cache.Layout, position.X, position.Y))
            throw new ArgumentException("地面操作位置无效");
        return new(position.X, position.Y);
    }
    /// <summary>合法候选落点确认后一次加入地图。</summary>
    public ValueTask<CreateGroundItemRt> HandleAsync(WorldContext context, CreateGroundItem request)
    {
        var origin = Position(context, request.Position);
        if (!context.Config.Item.Items.TryGetValue(request.ItemId, out var cfg) || request.Count <= 0 || request.Count > cfg.MaxStack || request.HasDropX != request.HasDropY)
            throw new ArgumentException("地面物品数量或落点无效");
        var point = request.HasDropX ? GroundDrop.Validate(request, context.Cache.Layout, origin) : GroundDrop.Choose(context.Cache.Layout, origin);
        var item = new DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 1, X = point.X, Y = point.Y, ItemId = request.ItemId, Count = request.Count };
        context.Cache.Objects.Commit(context.Cache.Objects.Value.Add(item)); Mark(context, item);
        return ValueTask.FromResult(new CreateGroundItemRt { Object = ToPb(item) });
    }
    /// <summary>只有当前 AOI 内实际距离不大于一瓦片的物品可被取回。</summary>
    public ValueTask<GetGroundItemRt> HandleAsync(WorldContext context, GetGroundItem request)
    {
        var origin = Position(context, request.Position);
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id)) throw new ArgumentException("地面对象编号无效");
        var item = context.Cache.Objects.Value.SingleOrDefault(item => item.ObjectId == id && item.MonsterTypeId is null)
            ?? throw new InvalidOperationException("地面物品已不存在");
        var layout = context.Cache.Layout; var target = new Vector2(item.X, item.Y);
        var center = context.Cache.Roles.Chunk(origin.X, origin.Y);
        if (!context.Cache.Roles.Neighborhood(center).Contains(context.Cache.Roles.Chunk(item.X, item.Y)) ||
            Vector2.DistanceSquared(origin, target) > (double)layout.TileSize * layout.TileSize)
            throw new InvalidOperationException("尚未到达拾取距离");
        return ValueTask.FromResult(new GetGroundItemRt { Object = ToPb(item) });
    }
    /// <summary>已在共享入口内校验的物品只允许删除一次。</summary>
    public ValueTask<RemoveGroundItemRt> HandleAsync(WorldContext context, RemoveGroundItem request)
    {
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id)) throw new ArgumentException("地面对象编号无效");
        var item = context.Cache.Objects.Value.SingleOrDefault(item => item.ObjectId == id && item.MonsterTypeId is null)
            ?? throw new InvalidOperationException("地面物品已不存在");
        context.Cache.Objects.Commit(context.Cache.Objects.Value.Remove(item)); Mark(context, item);
        return ValueTask.FromResult(new RemoveGroundItemRt());
    }
    /// <summary>静止观察者也必须收到所属块对象变化。</summary>
    private static void Mark(WorldContext context, DataWorldObject item) => context.Cache.Roles.ObjectsChanged(item.X, item.Y);
    /// <summary>为每次回复创建独立对象。</summary>
    internal static MapObjectPb ToPb(DataWorldObject item)
    {
        var value = new MapObjectPb { ObjectId = item.ObjectId.ToString("N"), Revision = item.Revision, X = item.X, Y = item.Y };
        if (item.MonsterTypeId is { } type) value.Monster = new() { MonsterTypeId = type };
        else value.GroundItem = new() { ItemId = item.ItemId, Count = item.Count };
        return value;
    }
}
