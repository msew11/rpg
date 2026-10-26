using Rpg.Const;

namespace Rpg.Utils;

/// <summary>集中维护已确定的道具大小类到装备槽位的代码映射。</summary>
public static class EquipmentSlotUtil
{
    /// <summary>武器统一推荐主手，盾牌推荐副手。</summary>
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

    /// <summary>按大小类校验单槽适配；完整装备还须检查双手冲突。</summary>
    public static bool CanEquip(ItemCategory category, ItemSubCategory subCategory, EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => category == ItemCategory.Weapon && subCategory is
            ItemSubCategory.Sword or ItemSubCategory.TwoHandedWeapon or ItemSubCategory.Staff or
            ItemSubCategory.Spear or ItemSubCategory.Katar or ItemSubCategory.ThrowingWeapon,
        EquipmentSlot.OffHand => category == ItemCategory.Armor && subCategory == ItemSubCategory.Shield,
        _ => false
    };

    /// <summary>普通剑之外的已支持武器占用双手，副手不得穿戴物品。</summary>
    public static bool OccupiesBothHands(ItemCategory category, ItemSubCategory subCategory) =>
        CanEquip(category, subCategory, EquipmentSlot.MainHand) && subCategory != ItemSubCategory.Sword;

    /// <summary>一件主手拳刃或暗器需要分别绘制源左右手持握。</summary>
    public static bool HasPairedAppearance(ItemCategory category, ItemSubCategory subCategory) =>
        category == ItemCategory.Weapon && subCategory is ItemSubCategory.Katar or ItemSubCategory.ThrowingWeapon;
}
