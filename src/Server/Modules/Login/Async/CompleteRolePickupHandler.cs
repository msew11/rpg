using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的拾取完成。</summary>
internal sealed class CompleteRolePickupHandler : IActorHandler<LoginContext, CompleteRolePickup, CompleteRolePickupRt>
{
    /// <summary>由运行时在共享物品入口内调度最终完成。</summary>
    public async ValueTask<CompleteRolePickupRt> HandleAsync(LoginContext context, CompleteRolePickup request) =>
        await context.Actors.Role(RoleItemCalls.Owned(context)).Ask<CompleteRolePickupRt>(request, context.Call).ConfigureAwait(false);
}
