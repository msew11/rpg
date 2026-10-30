using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Handler;

/// <summary>导出角色当前物品和公开装备。</summary>
internal sealed class GetRoleItemsHandler : IActorHandler<RoleContext, GetRoleItems, GetRoleItemsRt>
{
    /// <summary>只读导出最新完整物品与公开装备。</summary>
    public ValueTask<GetRoleItemsRt> HandleAsync(RoleContext context, GetRoleItems request) => ValueTask.FromResult(RoleItemRules.Create(context).Snapshot(context.Cache.Snapshot()));
}
