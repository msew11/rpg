using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>为脏观察者生成地图增量。</summary>
internal sealed class BuildWorldUpdatesHandler : IActorHandler<WorldContext, BuildWorldUpdates, BuildWorldUpdatesRt>
{
    /// <summary>只为脏观察者生成变化，无变化不产生空包。</summary>
    public ValueTask<BuildWorldUpdatesRt> HandleAsync(WorldContext context, BuildWorldUpdates request)
    {
        var reply = new BuildWorldUpdatesRt();
        foreach (var id in request.ObserverRoleIds)
            if (context.Cache.Roles.NeedsUpdate(id)) reply.Deliveries.Add(new WorldDeliveryPb { RoleId = id, Notice = new() { Frame = WorldFrames.Build(context, id, request.ServerTimeMs, false) } });
        return ValueTask.FromResult(reply);
    }
}
