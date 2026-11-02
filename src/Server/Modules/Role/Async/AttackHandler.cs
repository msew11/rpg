using System;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Modules.Common.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>验证每次起手并固定属性和服务器时序。</summary>
internal sealed class AttackHandler : IActorHandler<RoleContext, Attack, AttackRt>
{
    /// <summary>拒绝时返回原编号，客户端可以撤销对应预测。</summary>
    public async ValueTask<AttackRt> HandleAsync(RoleContext context, Attack request)
    {
        var cache = context.Cache; var now = context.Call!.ServerTimeMs;
        var reply = new AttackRt { AttackId = request.AttackId };
        if (request.AttackId == 0 || request.AttackId > long.MaxValue || request.AttackId <= Math.Max(cache.CombatInput, cache.Movement.Value.AcknowledgedAction)) { reply.Reason = "攻击输入已过期"; return reply; }
        cache.CombatInput = request.AttackId;
        cache.Movement.Advance(cache.Role, now, RoleAttributes.Speed(context));
        if (now < cache.NextAttackMs) { reply.Reason = "攻击间隔未结束"; return reply; }
        var attrs = cache.Attributes;
        var target = await context.Actors.World(cache.Role.Value.MapId).Ask<GetCombatTargetRt>(new GetCombatTarget {
            TargetId = request.TargetId, Position = RoleProjection.Build(context).Position, Range = attrs[9] }, context.Call).ConfigureAwait(false);
        if (target.Target is null) { reply.Reason = "目标已失效或不在攻击范围"; return reply; }
        await RoleCombat.CancelAsync(context).ConfigureAwait(false);
        cache.Pickup = null; cache.Movement.Halt();
        var main = cache.Items.Value.FirstOrDefault(item => item.Equipped && item.EquipmentSlot == Rpg.Const.EquipmentSlot.MainHand);
        var motion = main is null ? Rpg.Const.RoleMotion.Attack1 : context.Config.Equipment.Items[context.Config.Item.Items[main.ItemId].RelatedId].AttackMotion;
        var timing = context.Config.Basic.Value.AttackTiming(cache.Role.Value.Gender == Rpg.Server.Modules.Common.Data.RoleGender.Male ? Rpg.Const.RoleGender.Male : Rpg.Const.RoleGender.Female, motion);
        var duration = Rpg.Utils.CombatUtil.Duration(attrs[10]);
        var started = new AttackStartedNotice { MapId = cache.Role.Value.MapId, RoleId = cache.RoleId, AttackId = request.AttackId,
            TargetId = request.TargetId, StartTimeMs = now, DurationMs = duration, HitTimeMs = now + duration * timing.HitRatio,
            Motion = (int)motion, TargetX = target.Target.X, TargetY = target.Target.Y };
        cache.Attack = new(started, checked((int)attrs[7]), checked((int)attrs[8]), attrs[9]); cache.NextAttackMs = now + duration;
        await RoleProjectionSync.ProjectAsync(context).ConfigureAwait(false);
        await context.Actors.World(cache.Role.Value.MapId).Ask<QueueCombatEventRt>(new QueueCombatEvent { Event = new CombatEventPb { Started = started } }, context.Call).ConfigureAwait(false);
        reply.Accepted = true; reply.Started = started.Clone(); return reply;
    }
}
