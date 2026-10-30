using Rpg.Server.Modules.Login.Utils;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Handler;

/// <summary>在账号邮箱内校验并创建摘要，不定位角色或地图分片。</summary>
internal sealed class CreateRoleHandler : IActorHandler<LoginContext, CreateRole, CreateRoleRt>
{
    /// <summary>使用当前回合预留编号提交完整候选，再返回全部可见摘要。</summary>
    public ValueTask<CreateRoleRt> HandleAsync(LoginContext context, CreateRole request)
    {
        context.Cache.Summaries.Create(context.AllocatedRoleId, request.Name, (Common.Data.RoleGender)request.Gender, request.HeadId);
        var response = new CreateRoleRt();
        response.Roles.AddRange(RoleSummaries.Visible(context));
        return ValueTask.FromResult(response);
    }
}
