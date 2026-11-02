using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rpg.Protocol;

namespace Rpg.Server.Modules.Role.Utils;

/// <summary>等级和武器正常属性与 GM 执行速度的独立计算入口。</summary>
internal static class RoleAttributes
{
    /// <summary>等级或主手改变时重算正常属性，上限下降裁剪当前值。</summary>
    internal static void Refresh(RoleContext context)
    {
        var cache = context.Cache; var role = cache.Role.Value;
        var equipped = cache.Items.Value.FirstOrDefault(item => item.Equipped && item.EquipmentSlot == Rpg.Const.EquipmentSlot.MainHand);
        var weaponId = equipped?.ItemId ?? 0;
        if (cache.AttributeLevel == role.Level && cache.AttributeWeapon == weaponId) return;
        if (!context.Config.RoleLevel.Items.TryGetValue(role.Level, out var row)) throw new InvalidDataException($"缺少等级 {role.Level} 的基础属性");
        var values = row.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (weaponId != 0)
        {
            var weapon = context.Config.Equipment.Items[context.Config.Item.Items[weaponId].RelatedId];
            values[7] += weapon.BaseDamageMin; values[8] += weapon.BaseDamageMax;
            values[9] = weapon.BaseAttackRange; values[10] = weapon.BaseAttackSpeed;
        }
        foreach (var id in values.Keys.ToArray()) values[id] = context.Config.Attribute.Items[id].Normalize(values[id]);
        cache.Attributes = new ReadOnlyDictionary<int, double>(values); cache.AttributeLevel = role.Level;
        cache.AttributeWeapon = weaponId; cache.AttributesPending = true;
        var health = checked((int)values[2]); var mana = checked((int)values[3]);
        var next = role with { MaxHealth = health, MaxMana = mana, Health = Math.Min(role.Health, health), Mana = Math.Min(role.Mana, mana) };
        cache.Role.Commit(next); cache.Projection.Changed();
    }

    /// <summary>读取实际执行速度，GM 不修改正常属性。</summary>
    internal static double Speed(RoleContext context)
    {
        Refresh(context); var role = context.Cache.Role.Value;
        if (!double.IsFinite(role.GmMovementSpeed) || role.GmMovementSpeed < 0) throw new InvalidDataException("GM 速度必须为有限非负数");
        return role.GmMovementSpeedOverrideEnabled ? role.GmMovementSpeed : context.Cache.Attributes[1];
    }

    /// <summary>生成独占协议快照，不向消费者共享缓存。</summary>
    internal static AttributesPb Snapshot(RoleContext context)
    {
        Refresh(context); var result = new AttributesPb(); foreach (var pair in context.Cache.Attributes) result.Values.Add(pair.Key, pair.Value); return result;
    }

    /// <summary>生成本人完整私有数据，GM 设置与正常属性分别提供。</summary>
    internal static RolePb Private(RoleContext context)
    {
        var attributes = Snapshot(context); var role = context.Cache.Role.Value;
        return new() { Experience = role.Experience, NextExperience = role.NextExperience, BagExtraRows = role.BagExtraRows,
            Attributes = attributes, GmMovementSpeed = Gm(role.GmMovementSpeedOverrideEnabled, role.GmMovementSpeed) };
    }

    /// <summary>构造当前角色保存的调试设置。</summary>
    internal static GmMovementSpeedPb Gm(bool enabled, double speed) => new() { Enabled = enabled, Speed = speed };
}
