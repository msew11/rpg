using Rpg.Const;

namespace Rpg.Utils;

/// <summary>集中维护已确定的道具大小类到装备槽位的代码映射。</summary>
public static class EquipmentSlotUtil
{
    /// <summary>仅剑类武器绑定主手，其他组合返回未绑定。</summary>
    public static bool TryGetSlot(ItemCategory category, ItemSubCategory subCategory, out EquipmentSlot slot)
    {
        if (category == ItemCategory.Weapon && subCategory == ItemSubCategory.Sword)
        {
            slot = EquipmentSlot.MainHand;
            return true;
        }
        slot = default;
        return false;
    }
}
