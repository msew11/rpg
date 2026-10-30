using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的丢弃。</summary>
internal sealed class DropItemHandler : IActorHandler<LoginContext, DropItem, DropItemRt>
{
    /// <summary>直接编排已验证归属角色的丢弃。</summary>
    public async ValueTask<DropItemRt> HandleAsync(LoginContext context, DropItem request) =>
        await context.Actors.Role(RoleItemCalls.Owned(context)).Ask<DropItemRt>(request, context.Call).ConfigureAwait(false);
}
