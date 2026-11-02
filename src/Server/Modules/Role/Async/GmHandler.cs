using System;
using System.Globalization;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>按当前角色执行道具发放、调试移速与地图怪物创建。</summary>
internal sealed class GmHandler : IActorHandler<RoleContext, Gm, GmRt>
{
    /// <summary>校验命令，怪物交给当前地图创建，角色修改提交自身缓存与投影。</summary>
    public async ValueTask<GmRt> HandleAsync(RoleContext context, Gm request)
    {
        var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length > 0 && args[0] == "monster")
        {
            if (args.Length != 4 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var monsterId) ||
                !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                throw new ArgumentException("用法：/monster <怪物ID> <X> <Y>");
            var created = await context.Actors.World(context.Cache.Role.Value.MapId).Ask<Rpg.Server.ActorProtocol.CreateMonsterRt>(
                new Rpg.Server.ActorProtocol.CreateMonster { MonsterTypeId = monsterId, X = x, Y = y }, context.Call).ConfigureAwait(false);
            var value = created.Object;
            return new GmRt { RoleId = context.Cache.RoleId, Command = request.Command,
                Message = $"已放置 {context.Config.Monster.Items[monsterId].Name}（{monsterId}），坐标（{value.X.ToString("R", CultureInfo.InvariantCulture)}, {value.Y.ToString("R", CultureInfo.InvariantCulture)}）" };
        }
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
