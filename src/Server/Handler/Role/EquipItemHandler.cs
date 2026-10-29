using System;
using Rpg.Net.Message;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.Role;

/// <summary>校验物品信封版本后穿脱并返回同批私有快照。</summary>
public sealed class EquipItemHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public EquipItemRt Handle(RoleContext cache, EquipItem request)
    {
        var response = new EquipItemRt { RoleId = request.RoleId, Slot = request.Slot };
        try
        {
            var tables = cache.RequireRole(request.RoleId);
            if (request.ExpectedBagRevision != cache.Session.ItemsRevision || request.ExpectedEquipmentRevision != cache.Session.ItemsRevision)
                throw new InvalidOperationException("物品已更新，请重新选择装备");
            var candidate = cache.Rules.BagRules.Equip(tables, request.InstanceId, (Const.EquipmentSlot)request.Slot,
                request.HasTargetBagSlot ? request.TargetBagSlot : null);
            var notice = RoleMessages.Items(candidate);
            cache.Commit(candidate, 2);
            cache.Notices.Enqueue(notice);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
