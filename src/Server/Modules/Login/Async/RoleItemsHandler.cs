using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Login.Handler;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Async;

/// <summary>从可信控制角色检查账号归属，向角色编排物品操作。</summary>
internal sealed class RoleItemsHandler : IActorHandler<LoginContext, Gm, GmRt>,
    IActorHandler<LoginContext, MoveItem, MoveItemRt>, IActorHandler<LoginContext, EquipItem, EquipItemRt>, IActorHandler<LoginContext, DropItem, DropItemRt>,
    IActorHandler<LoginContext, PickupItem, PickupItemRt>, IActorHandler<LoginContext, CompleteRolePickup, CompleteRolePickupRt>
{
    /// <summary>角色身份只来自邮箱运行时元数据。</summary>
    private static long Owned(LoginContext context)
    { var id = context.Call?.RoleId ?? 0; context.Cache.Summaries.Owned(id); return id; }
    /// <summary>在账号已验证归属的前提下发放。</summary>
    public async ValueTask<GmRt> HandleAsync(LoginContext context, Gm request) =>
        await context.Actors.Role(Owned(context)).Ask<GmRt>(request, context.Call).ConfigureAwait(false);
    /// <summary>将原始移动协议直接交给角色邮箱。</summary>
    public async ValueTask<MoveItemRt> HandleAsync(LoginContext context, MoveItem request) =>
        await context.Actors.Role(Owned(context)).Ask<MoveItemRt>(request, context.Call).ConfigureAwait(false);
    /// <summary>穿脱已执行后按实际装备同步摘要，不依赖离开世界。</summary>
    public async ValueTask<EquipItemRt> HandleAsync(LoginContext context, EquipItem request)
    {
        var id = Owned(context);
        var reply = await context.Actors.Role(id).Ask<EquipItemRt>(request, context.Call).ConfigureAwait(false);
        var items = await context.Actors.Role(id).Ask<GetRoleItemsRt>(new GetRoleItems()).ConfigureAwait(false);
        var summary = RoleSummaries.Visible(context).Single(role => role.RoleId == id);
        context.Cache.Summaries.Synchronize(new RoleWorldStatePb { RoleId = id,
            Profile = new() { Name = summary.Name, Gender = summary.Gender, Level = summary.Level, HeadId = summary.HeadId }, Equipment = items.Equipment });
        return reply;
    }
    /// <summary>直接编排已验证归属角色的丢弃。</summary>
    public async ValueTask<DropItemRt> HandleAsync(LoginContext context, DropItem request) =>
        await context.Actors.Role(Owned(context)).Ask<DropItemRt>(request, context.Call).ConfigureAwait(false);
    /// <summary>立即接纳当前角色的拾取意图。</summary>
    public async ValueTask<PickupItemRt> HandleAsync(LoginContext context, PickupItem request) =>
        await context.Actors.Role(Owned(context)).Ask<PickupItemRt>(request, context.Call).ConfigureAwait(false);
    /// <summary>由运行时在共享物品入口内调度最终完成。</summary>
    public async ValueTask<CompleteRolePickupRt> HandleAsync(LoginContext context, CompleteRolePickup request) =>
        await context.Actors.Role(Owned(context)).Ask<CompleteRolePickupRt>(request, context.Call).ConfigureAwait(false);
}
