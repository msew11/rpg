using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Common.Utils;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>独立时钟推进及公开投影重试，不回滚已经接纳的移动。</summary>
internal sealed class AdvanceRoleHandler : IActorHandler<RoleContext, AdvanceRole, AdvanceRoleRt>
{
    /// <summary>推进一个权威时刻，不产生历史 Tick 积压。</summary>
    public async ValueTask<AdvanceRoleRt> HandleAsync(RoleContext context, AdvanceRole request)
    {
        context.Cache.Movement.Advance(context.Cache.Role, request.ServerTimeMs, request.MovementSpeed);
        await RoleProjectionSync.ProjectAsync(context).ConfigureAwait(false);
        var value = context.Cache.Movement.Value;
        return new AdvanceRoleRt { Moving = value.TargetX.HasValue || context.Cache.Projection.Value.Pending || context.Cache.Pickup is not null,
            PickupReady = PickupIntent.Ready(context) };
    }
}
