using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.Gm;

/// <summary>解析道具发放指令并原子提交实体候选。</summary>
public sealed class GmHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public GmRt Handle(RepoEntityCache cache, global::Rpg.Net.Message.Gm request)
    {
        var response = new GmRt { RoleId = request.RoleId, Command = request.Command };
        try
        {
            var tables = cache.RequireRole(request.RoleId);
            var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0 || args[0] != "item") throw new ArgumentException("未知 GM 指令；用法：/item <道具ID> [数量]");
            if (args.Length is < 2 or > 3 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId <= 0)
                throw new ArgumentException("用法：/item <正整数道具ID> [正整数数量]");
            var count = 1;
            if (args.Length == 3 && (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0))
                throw new ArgumentException("道具数量必须是有效的正整数");
            if (!cache.BagRules.Items.TryGetValue(itemId, out var definition)) throw new ArgumentException($"道具配置不存在：{itemId}");
            var candidate = cache.BagRules.Add(tables, itemId, count);
            var notice = RoleMessages.Items(candidate);
            cache.Commit(candidate, 2);
            cache.Notices.Enqueue(notice);
            response.Success = true;
            response.Message = $"已发放 {definition.Name}（{itemId}）× {count}";
        }
        catch (Exception error) { response.Message = error.Message; }
        return response;
    }
}
