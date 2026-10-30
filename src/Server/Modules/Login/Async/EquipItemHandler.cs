using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Login.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>编排账号所属角色的穿脱与摘要同步。</summary>
internal sealed class EquipItemHandler : IActorHandler<LoginContext, EquipItem, EquipItemRt>
{
    /// <summary>穿脱已执行后按实际装备同步摘要，不依赖离开世界。</summary>
    public async ValueTask<EquipItemRt> HandleAsync(LoginContext context, EquipItem request)
    {
        var id = RoleItemCalls.Owned(context);
        var reply = await context.Actors.Role(id).Ask<EquipItemRt>(request, context.Call).ConfigureAwait(false);
        var items = await context.Actors.Role(id).Ask<GetRoleItemsRt>(new GetRoleItems()).ConfigureAwait(false);
        var summary = RoleSummaries.Visible(context).Single(role => role.RoleId == id);
        context.Cache.Summaries.Synchronize(new RoleWorldStatePb { RoleId = id,
            Profile = new() { Name = summary.Name, Gender = summary.Gender, Level = summary.Level, HeadId = summary.HeadId }, Equipment = items.Equipment });
        return reply;
    }
}
