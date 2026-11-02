using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>地图邮箱内管理即时战斗事件。</summary>
internal sealed class QueueCombatEventHandler : IActorHandler<WorldContext, QueueCombatEvent, QueueCombatEventRt>
{
    /// <summary>按邮箱顺序捕获或取出完整事件。</summary>
    public ValueTask<QueueCombatEventRt> HandleAsync(WorldContext context, QueueCombatEvent request)
    { MonsterCombat.Queue(context, request.Event); return ValueTask.FromResult(new QueueCombatEventRt()); }
}
