using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Common.Utils;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>穿脱装备并向地图发布公开投影。</summary>
internal sealed class EquipItemHandler : IActorHandler<RoleContext, EquipItem, EquipItemRt>
{
    /// <summary>穿脱后标记独立公开投影，失败留待时钟重试。</summary>
    public async ValueTask<EquipItemRt> HandleAsync(RoleContext context, EquipItem request)
    {
        context.Cache.Commit(RoleItemRules.Create(context).Equip(context.Cache.Snapshot(), request.InstanceId, (Rpg.Const.EquipmentSlot)request.Slot,
            request.HasTargetBagSlot ? request.TargetBagSlot : null));
        context.Cache.Projection.Changed();
        await RoleProjectionSync.ProjectAsync(context).ConfigureAwait(false);
        return new EquipItemRt { RoleId = context.Cache.RoleId, Slot = request.Slot };
    }
}
