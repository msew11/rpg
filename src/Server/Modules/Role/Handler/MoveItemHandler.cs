using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Handler;

/// <summary>在角色邮箱内执行背包移动。</summary>
internal sealed class MoveItemHandler : IActorHandler<RoleContext, MoveItem, MoveItemRt>
{
    /// <summary>最新目标决定移动、合并或交换。</summary>
    public ValueTask<MoveItemRt> HandleAsync(RoleContext context, MoveItem request)
    {
        context.Cache.Commit(RoleItemRules.Create(context).Move(context.Cache.Snapshot(), request.InstanceId, request.TargetSlotIndex));
        return ValueTask.FromResult(new MoveItemRt { RoleId = context.Cache.RoleId, InstanceId = request.InstanceId, TargetSlotIndex = request.TargetSlotIndex });
    }
}
