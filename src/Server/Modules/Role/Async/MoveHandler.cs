using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
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
        await AdvanceRoleHandler.ProjectAsync(context).ConfigureAwait(false);
        return new MoveRt { ActionId = request.ActionId };
    }
}

/// <summary>独立时钟推进及公开投影重试，不回滚已经接纳的移动。</summary>
internal sealed class AdvanceRoleHandler : IActorHandler<RoleContext, AdvanceRole, AdvanceRoleRt>
{
    /// <summary>推进一个权威时刻，不产生历史 Tick 积压。</summary>
    public async ValueTask<AdvanceRoleRt> HandleAsync(RoleContext context, AdvanceRole request)
    {
        context.Cache.Movement.Advance(context.Cache.Role, request.ServerTimeMs, request.MovementSpeed);
        await ProjectAsync(context).ConfigureAwait(false);
        var value = context.Cache.Movement.Value;
        return new AdvanceRoleRt { Moving = value.TargetX.HasValue || context.Cache.Projection.Value.Pending || context.Cache.Pickup is not null,
            PickupReady = GroundItemsHandler.Ready(context) };
    }
    /// <summary>向 World 独占转交最新完整公开状态，失败留待下一轮重试。</summary>
    internal static async Task ProjectAsync(RoleContext context)
    {
        var cache = context.Cache; var movement = cache.Movement.Value;
        if (!cache.Projection.Value.Pending) return;
        try
        {
            await context.Actors.World(cache.Role.Value.MapId).Ask<UpdateWorldRoleRt>(new UpdateWorldRole
            { Role = Handler.RoleProjection.Build(context), Revision = cache.Projection.Value.Revision, AcknowledgedAction = movement.AcknowledgedAction },
                context.Call).ConfigureAwait(false);
            cache.Projection.Projected();
        }
        catch (OperationCanceledException) when (context.Call?.PreserveResult != true) { throw; }
        catch (Exception) { /* 投影仍待重试；不能撤销已经接受的移动。 */ }
    }
}
