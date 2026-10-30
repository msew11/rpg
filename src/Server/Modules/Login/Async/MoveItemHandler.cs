using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的背包移动。</summary>
internal sealed class MoveItemHandler : IActorHandler<LoginContext, MoveItem, MoveItemRt>
{
    /// <summary>将原始移动协议直接交给角色邮箱。</summary>
    public async ValueTask<MoveItemRt> HandleAsync(LoginContext context, MoveItem request) =>
        await context.Actors.Role(RoleItemCalls.Owned(context)).Ask<MoveItemRt>(request, context.Call).ConfigureAwait(false);
}
