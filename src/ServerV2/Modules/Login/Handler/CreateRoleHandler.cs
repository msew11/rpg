using System.Threading.Tasks;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login.Handler;

/// <summary>在账号邮箱内校验并创建摘要，不定位角色或地图分片。</summary>
internal sealed class CreateRoleHandler : IActorHandler<LoginContext, CreateRole, CreateRoleRt>
{
    /// <summary>先校验完整候选，再一次提交；失败不消耗编号或留下部分状态。</summary>
    public ValueTask<CreateRoleRt> HandleAsync(LoginContext context, CreateRole request)
    {
        context.Cache.Summaries.Create(request.Name, (Common.Data.RoleGender)request.Gender, request.HeadId, DefaultAccounts.FirstRoleId(context.Cache.Username));
        var response = new CreateRoleRt();
        response.Roles.AddRange(RoleSummaries.Visible(context));
        return ValueTask.FromResult(response);
    }
}
