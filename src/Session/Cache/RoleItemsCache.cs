using Rpg.Protocol;
using Rpg.Services;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>按 TCP 顺序验证并接纳 完整物品，生命周期随账号。</summary>
internal sealed class RoleItemsCache : CacheBase
{
    /// <summary>所属角色缓存。</summary>
    private readonly RoleCache _role;
    /// <summary>所属账号摘要缓存。</summary>
    private readonly LoginCache _login;
    /// <summary>建立缓存阶段监听，不持有网络或协议对象。</summary>
    internal RoleItemsCache(LoginCache login, RoleCache role)
    { _login = login; _role = role; Subscribe<RoleItemsChangedNotice>(Accept); }
    /// <summary>完整校验两侧后连续提交，无物品版本条件。</summary>
    private void Accept(RoleItemsChangedNotice notice)
    {
        if (!_role.IsInitialized || notice.RoleId != _role.RoleId) return;
        var config = GameServices.Config;
        var bag = BagPbData.FromPb(notice.Bag, config.Basic.Value.GetBagCapacity(_role.State.BagExtraRows), config.Item.Items);
        if (notice.Equipment is null) throw new System.IO.InvalidDataException("物品通知缺少装备");
        // 此计数仅驱动既有 ECS 外观动作，不作为协议或业务条件。
        var equipment = RoleWorldEquipmentPbData.FromItems(notice.RoleId, _role.Equipment.Revision + 1, notice.Equipment.EquippedItems, config.Item.Items);
        equipment.ValidateBag(bag);
        _role.AcceptItems(bag, equipment); _login.RefreshRole(_role.State);
    }
}
