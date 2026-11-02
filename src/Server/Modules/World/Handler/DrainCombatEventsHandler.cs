using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>地图邮箱内管理即时战斗事件。</summary>
internal sealed class DrainCombatEventsHandler : IActorHandler<WorldContext, DrainCombatEvents, DrainCombatEventsRt>
{
    /// <summary>按邮箱顺序捕获或取出完整事件。</summary>
    public ValueTask<DrainCombatEventsRt> HandleAsync(WorldContext context, DrainCombatEvents request)
    { var reply = new DrainCombatEventsRt(); while (context.Cache.CombatEvents.TryDequeue(out var item)) reply.Deliveries.Add(item); return ValueTask.FromResult(reply); }
}
