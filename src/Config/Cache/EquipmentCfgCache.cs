using System.IO;
using Godot;
using Rpg.Config.Data;
using Rpg.Const;

namespace Rpg.Config.Cache;

/// <summary>保存独立装备配置，并在全表就绪后检查所有装备道具的引用。</summary>
[CfgFile("equipment.json")]
public sealed class EquipmentCfgCache : MultiCfgCache<int, EquipmentCfg>
{
    /// <summary>由配置服务创建并拥有装备缓存。</summary>
    internal EquipmentCfgCache() { }

    /// <summary>持握素材必须随程序发布，错误外观引用在启动时失败。</summary>
    protected override void Validate(EquipmentCfg cfg)
    {
        var metadata = Godot.FileAccess.GetFileAsString($"res://assets/characters/weapons/{cfg.AppearanceId}/male/stand.tres");
        var attack = System.Text.RegularExpressions.Regex.Match(metadata, @"(?m)^AttackMotion = (\d+)");
        cfg.AttackMotion = attack.Success ? (RoleMotion)int.Parse(attack.Groups[1].Value) : RoleMotion.Stand;
        foreach (var gender in new[] { "male", "female" })
        foreach (var motion in System.Enum.GetValues<RoleMotion>())
        {
            var path = $"res://assets/characters/weapons/{cfg.AppearanceId}/{gender}/{motion.ToString().ToLowerInvariant()}.tres";
            if (!ResourceLoader.Exists(path)) throw new InvalidDataException($"equipment.json id={cfg.Id} 外观资源不存在：{path}");
        }
    }

    /// <summary>使用大类索引检查武器和防具的关联，不按是否已绑定槽位筛选。</summary>
    protected override void Validate(ConfigService config)
    {
        foreach (var category in new[] { ItemCategory.Weapon, ItemCategory.Armor })
        foreach (var item in config.Item.GetByCategory(category))
        {
            if (Items.TryGetValue(item.RelatedId, out var weapon) && category == ItemCategory.Weapon)
            {
                if (weapon.AttackMotion is not (RoleMotion.Attack1 or RoleMotion.Attack2 or RoleMotion.Attack3))
                    throw new InvalidDataException("武器缺少原生攻击类别");
                config.Attribute.Items[7].ValidateValue(weapon.BaseDamageMin);
                config.Attribute.Items[8].ValidateValue(weapon.BaseDamageMax);
                config.Attribute.Items[9].ValidateValue(weapon.BaseAttackRange);
                config.Attribute.Items[10].ValidateValue(weapon.BaseAttackSpeed);
                if (weapon.BaseDamageMin > weapon.BaseDamageMax) throw new InvalidDataException("武器点伤下限不能超过上限");
            }
            if (!Items.ContainsKey(item.RelatedId))
                throw new InvalidDataException($"item.json 道具 id={item.Id} 的 relatedId={item.RelatedId} 不存在于 equipment.json");
        }
    }
}
