using System;
using System.Linq;
using Rpg.Net.Message;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.Role;

/// <summary>在实体候选内移动、合并或交换背包格子。</summary>
public sealed class MoveItemHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public MoveItemRt Handle(RoleContext cache, MoveItem request)
    {
        var response = new MoveItemRt { RoleId = request.RoleId, InstanceId = request.InstanceId, TargetSlotIndex = request.TargetSlotIndex };
        try
        {
            var tables = cache.RequireRole(request.RoleId);
            if (request.ExpectedRevision != cache.Session.ItemsRevision) throw new InvalidOperationException("背包已更新，请重新拖动物品");
            cache.Rules.BagRules.Validate(tables);
            if (request.TargetSlotIndex < 0 || request.TargetSlotIndex >= cache.Rules.BagRules.GetCapacity(tables.Role.Value.BagExtraRows))
                throw new ArgumentException("目标格子超出背包范围");
            if (!tables.Item.Items.TryGetValue(request.InstanceId, out var source) || source.Equipped) throw new ArgumentException("源物品不存在");
            if (source.SlotIndex == request.TargetSlotIndex) { response.Success = true; return response; }
            var target = tables.Item.Items.Values.SingleOrDefault(item => !item.Equipped && item.SlotIndex == request.TargetSlotIndex);
            var items = tables.Item.Items.Values.ToDictionary(item => item.InstanceId);
            items.Remove(source.InstanceId);
            if (target is null) items[source.InstanceId] = source with { SlotIndex = request.TargetSlotIndex };
            else
            {
                var maximum = cache.Rules.BagRules.Items[source.ItemId].MaxStack;
                if (source.ItemId == target.ItemId && target.Count < maximum)
                {
                    var transferred = Math.Min(source.Count, maximum - target.Count);
                    items[target.InstanceId] = target with { Count = target.Count + transferred };
                    if (source.Count > transferred) items[source.InstanceId] = source with { Count = source.Count - transferred };
                }
                else
                {
                    items[source.InstanceId] = source with { SlotIndex = target.SlotIndex };
                    items[target.InstanceId] = target with { SlotIndex = source.SlotIndex };
                }
            }
            var candidate = tables.WithItems(items.Values);
            cache.Rules.BagRules.Validate(candidate);
            var notice = RoleMessages.Items(candidate);
            cache.Commit(candidate, 2);
            cache.Notices.Enqueue(notice);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
