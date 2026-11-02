using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>在唯一地图邮箱内原子随机扣血，避免并发重复死亡。</summary>
internal sealed class HitMonsterHandler : IActorHandler<WorldContext, HitMonster, HitMonsterRt>
{
    /// <summary>无效或已经死亡的目标不产生伤害事件。</summary>
    public ValueTask<HitMonsterRt> HandleAsync(WorldContext context, HitMonster request)
    {
        var target = MonsterCombat.Target(context, request.TargetId, request.Position, request.Range);
        if (target is null) return ValueTask.FromResult(new HitMonsterRt());
        var damage = Rpg.Utils.CombatUtil.Roll(request.DamageMin, request.DamageMax, Random.Shared);
        var next = target with { Health = Math.Max(0, target.Health!.Value - damage), Revision = checked(target.Revision + 1) };
        context.Cache.Objects.Commit(next.Health == 0 ? context.Cache.Objects.Value.Remove(target) : context.Cache.Objects.Value.Replace(target, next));
        GroundItems.Mark(context, target);
        MonsterCombat.Queue(context, new CombatEventPb { Hit = new AttackHitNotice { MapId = context.Cache.MapId, RoleId = request.RoleId,
            AttackId = request.AttackId, Target = GroundItems.ToPb(next), Damage = damage, Dead = next.Health == 0, ServerTimeMs = request.ServerTimeMs } });
        return ValueTask.FromResult(new HitMonsterRt { Hit = true });
    }
}
