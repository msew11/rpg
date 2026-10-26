using Rpg.Const;
using Rpg.Utils;

namespace ClientTests;

/// <summary>验证 equipment-config 的十一槽枚举及已确认主副手规则。</summary>
public sealed class EquipmentSlotTests
{
    /// <summary>全组合验证未知类型不适配，拳刃允许主副手，盾牌和暗器仅副手。</summary>
    [Fact]
    public void CategoriesMapToConfirmedHands()
    {
        Assert.Equal(11, Enum.GetValues<EquipmentSlot>().Distinct().Count());
        Assert.NotEqual(EquipmentSlot.Ring1, EquipmentSlot.Ring2);
        foreach (var category in Enum.GetValues<ItemCategory>())
        foreach (var subCategory in Enum.GetValues<ItemSubCategory>())
        {
            var bound = EquipmentSlotUtil.TryGetSlot(category, subCategory, out var slot);
            var main = category == ItemCategory.Weapon && subCategory is ItemSubCategory.Sword or
                ItemSubCategory.Spear or ItemSubCategory.TwoHandedWeapon or ItemSubCategory.Staff or ItemSubCategory.Katar;
            var off = category == ItemCategory.Weapon && subCategory is ItemSubCategory.Katar or ItemSubCategory.ThrowingWeapon ||
                category == ItemCategory.Armor && subCategory == ItemSubCategory.Shield;
            Assert.Equal(main || off, bound);
            Assert.Equal(main, EquipmentSlotUtil.CanEquip(category, subCategory, EquipmentSlot.MainHand));
            Assert.Equal(off, EquipmentSlotUtil.CanEquip(category, subCategory, EquipmentSlot.OffHand));
            if (bound) Assert.Equal(main ? EquipmentSlot.MainHand : EquipmentSlot.OffHand, slot);
            foreach (var other in Enum.GetValues<EquipmentSlot>().Where(value => value is not (EquipmentSlot.MainHand or EquipmentSlot.OffHand)))
                Assert.False(EquipmentSlotUtil.CanEquip(category, subCategory, other));
        }
        Assert.False(EquipmentSlotUtil.TryGetSlot((ItemCategory)999, ItemSubCategory.Sword, out _));
        Assert.False(EquipmentSlotUtil.TryGetSlot(ItemCategory.Weapon, (ItemSubCategory)999, out _));
    }
}
