using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Login.Handler;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>在账号邮箱内完成归属、角色准备和摘要同步，无反向 Ask。</summary>
internal sealed class EnterWorldHandler : IActorHandler<LoginContext, EnterWorld, EnterWorldRt>
{
    /// <summary>只在归属成功后定位角色，实际状态在本次操作同步回摘要。</summary>
    public async ValueTask<EnterWorldRt> HandleAsync(LoginContext context, EnterWorld request)
    {
        context.Cache.Summaries.Owned(request.RoleId);
        var summary = RoleSummaries.Visible(context).Single(role => role.RoleId == request.RoleId);
        var prepared = await context.Actors.Role(request.RoleId).Ask<PrepareRoleRt>(new PrepareRole { Summary = summary }, context.Call).ConfigureAwait(false);
        context.Cache.Summaries.Synchronize(prepared.State);
        return new EnterWorldRt { State = prepared.State, Role = prepared.Role, Bag = prepared.Bag, Map = prepared.Map };
    }
}
