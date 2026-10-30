using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Handler;

/// <summary>仅在账号摘要中保存删除墓碑，不接触角色三表。</summary>
internal sealed class DeleteRoleHandler : IActorHandler<LoginContext, DeleteRole, DeleteRoleRt>
{
    /// <summary>编号必须属于本账号可见摘要；墓碑保留以避免重启后复用编号。</summary>
    public ValueTask<DeleteRoleRt> HandleAsync(LoginContext context, DeleteRole request)
    {
        context.Cache.Summaries.Delete(request.RoleId);
        var response = new DeleteRoleRt();
        response.Roles.AddRange(RoleSummaries.Visible(context));
        return ValueTask.FromResult(response);
    }
}
