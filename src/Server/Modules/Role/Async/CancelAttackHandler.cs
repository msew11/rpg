using Rpg.Server.Modules.Common.Utils;
using System;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>顺序接纳显式取消，旧取消不能打断新攻击。</summary>
internal sealed class CancelAttackHandler : IActorHandler<RoleContext, CancelAttack, CancelAttackRt>
{
    /// <summary>前摇取消不结算，后摇取消不回滚。</summary>
    public async ValueTask<CancelAttackRt> HandleAsync(RoleContext context, CancelAttack request)
    {
        if (request.ActionId > Math.Max(context.Cache.CombatInput, context.Cache.Movement.Value.AcknowledgedAction))
        { context.Cache.CombatInput = request.ActionId; await RoleCombat.CancelAsync(context).ConfigureAwait(false); }
        return new CancelAttackRt { ActionId = request.ActionId };
    }
}
