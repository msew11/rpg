using Rpg.Const;

namespace Rpg.Utils;

/// <summary>集中维护已确定的道具大小类到装备槽位的代码映射。</summary>
public static class EquipmentSlotUtil
{
    /// <summary>返回默认放置槽位，拳刃默认主手，显式选择时两手均可。</summary>
    public static bool TryGetSlot(ItemCategory category, ItemSubCategory subCategory, out EquipmentSlot slot)
    {
        if (CanEquip(category, subCategory, EquipmentSlot.MainHand))
        {
            slot = EquipmentSlot.MainHand;
            return true;
        }
        if (CanEquip(category, subCategory, EquipmentSlot.OffHand))
        {
            slot = EquipmentSlot.OffHand;
            return true;
        }
        slot = default;
        return false;
    }

    /// <summary>按已确认的大小类校验目标手别，不隐含双手占槽规则。</summary>
    public static bool CanEquip(ItemCategory category, ItemSubCategory subCategory, EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => category == ItemCategory.Weapon && subCategory is
            ItemSubCategory.Sword or ItemSubCategory.TwoHandedWeapon or ItemSubCategory.Staff or
            ItemSubCategory.Spear or ItemSubCategory.Katar,
        EquipmentSlot.OffHand => category == ItemCategory.Weapon && subCategory is
            ItemSubCategory.ThrowingWeapon or ItemSubCategory.Katar ||
            category == ItemCategory.Armor && subCategory == ItemSubCategory.Shield,
        _ => false
    };
}
