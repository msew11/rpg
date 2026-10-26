using Rpg.Const;
using Rpg.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/equipment-config 的十一种槽位与仅剑类主手绑定契约。</summary>
public sealed class EquipmentSlotTests
{
    /// <summary>全组合验证不会把其他武器、盾牌或错配分类意外绑定为主手。</summary>
    [Fact]
    public void OnlyWeaponSwordMapsToMainHand()
    {
        Assert.Equal(11, Enum.GetValues<EquipmentSlot>().Distinct().Count());
        Assert.NotEqual(EquipmentSlot.Ring1, EquipmentSlot.Ring2);
        foreach (var category in Enum.GetValues<ItemCategory>())
        foreach (var subCategory in Enum.GetValues<ItemSubCategory>())
        {
            var bound = EquipmentSlotUtil.TryGetSlot(category, subCategory, out var slot);
            Assert.Equal(category == ItemCategory.Weapon && subCategory == ItemSubCategory.Sword, bound);
            if (bound) Assert.Equal(EquipmentSlot.MainHand, slot);
        }
        Assert.False(EquipmentSlotUtil.TryGetSlot((ItemCategory)999, ItemSubCategory.Sword, out _));
        Assert.False(EquipmentSlotUtil.TryGetSlot(ItemCategory.Weapon, (ItemSubCategory)999, out _));
    }
}
