using System;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>在地图邮箱内删除已校验的地面物品。</summary>
internal sealed class RemoveGroundItemHandler : IActorHandler<WorldContext, RemoveGroundItem, RemoveGroundItemRt>
{
    /// <summary>已在共享入口内校验的物品只允许删除一次。</summary>
    public ValueTask<RemoveGroundItemRt> HandleAsync(WorldContext context, RemoveGroundItem request)
    {
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id)) throw new ArgumentException("地面对象编号无效");
        var item = context.Cache.Objects.Value.SingleOrDefault(item => item.ObjectId == id && item.MonsterTypeId is null)
            ?? throw new InvalidOperationException("地面物品已不存在");
        context.Cache.Objects.Commit(context.Cache.Objects.Value.Remove(item)); GroundItems.Mark(context, item);
        return ValueTask.FromResult(new RemoveGroundItemRt());
    }
}
