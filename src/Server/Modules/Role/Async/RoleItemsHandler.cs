using System;
using System.Globalization;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Role.Handler;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>角色邮箱内按最新三表执行物品命令，不接收客户端版本。</summary>
internal sealed class RoleItemsHandler : IActorHandler<RoleContext, Gm, GmRt>,
    IActorHandler<RoleContext, MoveItem, MoveItemRt>, IActorHandler<RoleContext, EquipItem, EquipItemRt>,
    IActorHandler<RoleContext, GetRoleItems, GetRoleItemsRt>
{
    /// <summary>每回合从所属配置建立独立规则，不持有服务依赖。</summary>
    internal static ItemRules Rules(RoleContext context) => new(context.Config.Basic.Value.BagColumns,
        context.Config.Basic.Value.BagRows, context.Config.Item.Items.Values);
    /// <summary>校验命令并原子发放，默认数量为一。</summary>
    public ValueTask<GmRt> HandleAsync(RoleContext context, Gm request)
    {
        var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length is < 2 or > 3 || args[0] != "item" || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new ArgumentException("用法：/item <道具ID> [数量]");
        var count = 1;
        if (args.Length == 3 && !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out count)) throw new ArgumentException("数量必须为正整数");
        var rules = Rules(context); var candidate = rules.Add(context.Cache.Snapshot(), id, count);
        context.Cache.Commit(candidate);
        return ValueTask.FromResult(new GmRt { RoleId = context.Cache.RoleId, Command = request.Command, Message = $"已发放 {rules.Items[id].Name}（{id}）× {count}" });
    }
    /// <summary>最新目标决定移动、合并或交换。</summary>
    public ValueTask<MoveItemRt> HandleAsync(RoleContext context, MoveItem request)
    {
        context.Cache.Commit(Rules(context).Move(context.Cache.Snapshot(), request.InstanceId, request.TargetSlotIndex));
        return ValueTask.FromResult(new MoveItemRt { RoleId = context.Cache.RoleId, InstanceId = request.InstanceId, TargetSlotIndex = request.TargetSlotIndex });
    }
    /// <summary>穿脱后标记独立公开投影，失败留待时钟重试。</summary>
    public async ValueTask<EquipItemRt> HandleAsync(RoleContext context, EquipItem request)
    {
        context.Cache.Commit(Rules(context).Equip(context.Cache.Snapshot(), request.InstanceId, (Rpg.Const.EquipmentSlot)request.Slot,
            request.HasTargetBagSlot ? request.TargetBagSlot : null));
        context.Cache.Projection.Changed();
        await AdvanceRoleHandler.ProjectAsync(context).ConfigureAwait(false);
        return new EquipItemRt { RoleId = context.Cache.RoleId, Slot = request.Slot };
    }
    /// <summary>只读导出最新完整物品与公开装备。</summary>
    public ValueTask<GetRoleItemsRt> HandleAsync(RoleContext context, GetRoleItems request) => ValueTask.FromResult(Rules(context).Snapshot(context.Cache.Snapshot()));
}
