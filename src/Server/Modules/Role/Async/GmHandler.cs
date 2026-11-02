using System;
using System.Globalization;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>按最新角色快照执行道具发放与调试移速。</summary>
internal sealed class GmHandler : IActorHandler<RoleContext, Gm, GmRt>
{
    /// <summary>校验命令并提交角色候选，移速变更后同步公开投影。</summary>
    public async ValueTask<GmRt> HandleAsync(RoleContext context, Gm request)
    {
        var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length > 0 && args[0] == "movespeed")
        {
            if (args.Length != 2) throw new ArgumentException("用法：/movespeed <非负数|off>");
            var enabled = args[1] != "off";
            var speed = context.Cache.Role.Value.GmMovementSpeed;
            if (enabled && (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out speed) || !double.IsFinite(speed) || speed < 0))
                throw new ArgumentException("移动速度必须为有限非负数");
            var now = context.Call!.ServerTimeMs;
            context.Cache.Movement.Advance(context.Cache.Role, now, RoleAttributes.Speed(context));
            context.Cache.Role.Commit(context.Cache.Role.Value with { GmMovementSpeedOverrideEnabled = enabled, GmMovementSpeed = speed });
            context.Cache.Movement.Advance(context.Cache.Role, now, RoleAttributes.Speed(context));
            await Rpg.Server.Modules.Common.Utils.RoleProjectionSync.ProjectAsync(context).ConfigureAwait(false);
            return new GmRt { RoleId = context.Cache.RoleId, Command = request.Command,
                Message = enabled ? $"已启用 GM 移速 {speed.ToString("R", CultureInfo.InvariantCulture)}" : "已恢复属性移速",
                MovementSpeed = RoleAttributes.Gm(enabled, speed) };
        }
        if (args.Length is < 2 or > 3 || args[0] != "item" || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new ArgumentException("用法：/item <道具ID> [数量]");
        var count = 1;
        if (args.Length == 3 && !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out count)) throw new ArgumentException("数量必须为正整数");
        var rules = RoleItemRules.Create(context); var candidate = rules.Add(context.Cache.Snapshot(), id, count);
        context.Cache.Commit(candidate);
        return new GmRt { RoleId = context.Cache.RoleId, Command = request.Command, Message = $"已发放 {rules.Items[id].Name}（{id}）× {count}" };
    }
}
