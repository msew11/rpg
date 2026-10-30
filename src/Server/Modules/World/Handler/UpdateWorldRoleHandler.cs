using System;
using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>接纳当前地图的角色公开投影。</summary>
internal sealed class UpdateWorldRoleHandler : IActorHandler<WorldContext, UpdateWorldRole, UpdateWorldRoleRt>
{
    /// <summary>只接纳所属地图和较新版本的不可变角色公开数据。</summary>
    public ValueTask<UpdateWorldRoleRt> HandleAsync(WorldContext context, UpdateWorldRole request)
    {
        if (request.Role?.Position is null || request.Role.Position.MapId != context.Cache.MapId) throw new InvalidOperationException("公开投影地图不一致");
        context.Cache.Roles.Update(DataWorldRole.FromPb(request.Role, request.Revision, request.AcknowledgedAction));
        return ValueTask.FromResult(new UpdateWorldRoleRt());
    }
}
