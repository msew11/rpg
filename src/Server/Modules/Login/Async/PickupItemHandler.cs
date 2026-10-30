using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的拾取意图。</summary>
internal sealed class PickupItemHandler : IActorHandler<LoginContext, PickupItem, PickupItemRt>
{
    /// <summary>立即接纳当前角色的拾取意图。</summary>
    public async ValueTask<PickupItemRt> HandleAsync(LoginContext context, PickupItem request) =>
        await context.Actors.Role(RoleItemCalls.Owned(context)).Ask<PickupItemRt>(request, context.Call).ConfigureAwait(false);
}
