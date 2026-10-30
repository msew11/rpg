using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的GM 发放。</summary>
internal sealed class GmHandler : IActorHandler<LoginContext, Gm, GmRt>
{
    /// <summary>在账号已验证归属的前提下发放。</summary>
    public async ValueTask<GmRt> HandleAsync(LoginContext context, Gm request) =>
        await context.Actors.Role(RoleItemCalls.Owned(context)).Ask<GmRt>(request, context.Call).ConfigureAwait(false);
}
