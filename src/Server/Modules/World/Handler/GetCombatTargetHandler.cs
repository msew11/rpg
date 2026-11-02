using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>查询攻击目标，不改变地图状态。</summary>
internal sealed class GetCombatTargetHandler : IActorHandler<WorldContext, GetCombatTarget, GetCombatTargetRt>
{
    /// <summary>只有活着且实际在范围内的怪物可起手。</summary>
    public ValueTask<GetCombatTargetRt> HandleAsync(WorldContext context, GetCombatTarget request) => ValueTask.FromResult(
        new GetCombatTargetRt { Target = MonsterCombat.Target(context, request.TargetId, request.Position, request.Range) is { } target ? GroundItems.ToPb(target) : null });
}
