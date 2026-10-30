using System;
using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Utils;

namespace Rpg.Server.Modules.Common.Utils;

/// <summary>为角色命令与时钟共用公开投影发布和重试。</summary>
internal static class RoleProjectionSync
{
    /// <summary>向 World 独占转交最新完整公开状态，失败留待下一轮重试。</summary>
    internal static async Task ProjectAsync(RoleContext context)
    {
        var cache = context.Cache; var movement = cache.Movement.Value;
        if (!cache.Projection.Value.Pending) return;
        try
        {
            await context.Actors.World(cache.Role.Value.MapId).Ask<UpdateWorldRoleRt>(new UpdateWorldRole
            { Role = RoleProjection.Build(context), Revision = cache.Projection.Value.Revision, AcknowledgedAction = movement.AcknowledgedAction },
                context.Call).ConfigureAwait(false);
            cache.Projection.Projected();
        }
        catch (OperationCanceledException) when (context.Call?.PreserveResult != true) { throw; }
        catch (Exception) { /* 投影仍待重试；不能撤销已经接受的移动。 */ }
    }
}
