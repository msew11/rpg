using System;
using System.Globalization;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role.Handler;

/// <summary>按最新角色快照执行 GM 发放。</summary>
internal sealed class GmHandler : IActorHandler<RoleContext, Gm, GmRt>
{
    /// <summary>校验命令并原子发放，默认数量为一。</summary>
    public ValueTask<GmRt> HandleAsync(RoleContext context, Gm request)
    {
        var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length is < 2 or > 3 || args[0] != "item" || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new ArgumentException("用法：/item <道具ID> [数量]");
        var count = 1;
        if (args.Length == 3 && !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out count)) throw new ArgumentException("数量必须为正整数");
        var rules = RoleItemRules.Create(context); var candidate = rules.Add(context.Cache.Snapshot(), id, count);
        context.Cache.Commit(candidate);
        return ValueTask.FromResult(new GmRt { RoleId = context.Cache.RoleId, Command = request.Command, Message = $"已发放 {rules.Items[id].Name}（{id}）× {count}" });
    }
}
