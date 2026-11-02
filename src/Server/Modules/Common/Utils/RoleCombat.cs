using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;

using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Utils;

namespace Rpg.Server.Modules.Common.Utils;

/// <summary>角色邮箱独占的攻击阶段推进，只向地图调用。</summary>
internal static class RoleCombat
{
    /// <summary>取消尚未完成的动作，保留冷却避免取消后摇加速。</summary>
    internal static async ValueTask CancelAsync(RoleContext context)
    {
        if (context.Cache.Attack is not { } attack) return;
        context.Cache.Attack = null;
        await context.Actors.World(context.Cache.Role.Value.MapId).Ask<QueueCombatEventRt>(new QueueCombatEvent { Event = new CombatEventPb {
            Cancelled = new AttackCancelledNotice { MapId = context.Cache.Role.Value.MapId, RoleId = context.Cache.RoleId, AttackId = attack.Started.AttackId, StartTimeMs = attack.Started.StartTimeMs } } }, context.Call).ConfigureAwait(false);
    }
    /// <summary>在时钟轮次命中一次，结束后保留下一次截止时间。</summary>
    internal static async ValueTask AdvanceAsync(RoleContext context, long now)
    {
        var cache = context.Cache;
        if (cache.Attack is not { } attack) return;
        if (!attack.HitApplied && now >= attack.Started.HitTimeMs)
        {
            cache.Attack = attack = attack with { HitApplied = true };
            var result = await context.Actors.World(cache.Role.Value.MapId).Ask<HitMonsterRt>(new HitMonster {
                RoleId = cache.RoleId, AttackId = attack.Started.AttackId, TargetId = attack.Started.TargetId,
                Position = RoleProjection.Build(context).Position, Range = attack.Range,
                DamageMin = attack.DamageMin, DamageMax = attack.DamageMax, ServerTimeMs = now }, context.Call).ConfigureAwait(false);
            if (!result.Hit) { await CancelAsync(context).ConfigureAwait(false); return; }
        }
        if (now >= cache.NextAttackMs) cache.Attack = null;
    }
}
