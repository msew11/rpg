namespace Rpg.Const;

/// <summary>当前已确认的道具小类原码，含义须结合大类判断，不作为位标记。</summary>
public enum ItemSubCategory
{
    /// <summary>枪类武器。</summary>
    Spear = 1,
    /// <summary>剑类武器。</summary>
    Sword = 2,
    /// <summary>双手武器或工具类。</summary>
    TwoHandedWeapon = 4,
    /// <summary>拳刃类武器。</summary>
    Katar = 8,
    /// <summary>法杖类武器。</summary>
    Staff = 16,
    /// <summary>暗器类武器。</summary>
    ThrowingWeapon = 32,
    /// <summary>盾牌类防具。</summary>
    Shield = 105
}
