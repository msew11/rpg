using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Common.Utils;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>在权威邮箱内接纳目标，提交后向所属地图投影。</summary>
internal sealed class MoveHandler : IActorHandler<RoleContext, Move, MoveRt>
{
    /// <summary>校验目标、物化旧移动并接纳最新动作。</summary>
    public async ValueTask<MoveRt> HandleAsync(RoleContext context, Move request)
    {
        var cache = context.Cache; var data = cache.Role.Value; var map = context.Config.Map.Items[data.MapId];
        if (request.ActionId == 0 || request.Target is not { } target || !float.IsFinite(target.X) || !float.IsFinite(target.Y) ||
            target.X < 0 || target.Y < 0 || target.X >= (long)map.Width * map.TileSize || target.Y >= (long)map.Height * map.TileSize)
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "移动目标或动作编号无效");
        if (request.ActionId > cache.Movement.Value.AcknowledgedAction) cache.Pickup = null;
        cache.Movement.Advance(cache.Role, context.Call!.ServerTimeMs, context.Call.MovementSpeed);
        cache.Movement.Move(cache.Role, request.ActionId, target.X, target.Y);
        await RoleProjectionSync.ProjectAsync(context).ConfigureAwait(false);
        return new MoveRt { ActionId = request.ActionId };
    }
}
