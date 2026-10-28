using System;
using System.IO;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>仅持有 role.json 对应的单个角色实体。</summary>
public sealed class RoleEntityCache
{
    /// <summary>角色单例表的当前实体。</summary>
    public RoleEntity Value { get; }

    /// <summary>绑定独立角色实体，不持有其他表缓存。</summary>
    public RoleEntityCache(RoleEntity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }
    /// <summary>先诊断角色领域自身字段，避免将角色损坏归因到背包路径。</summary>
    public static void Validate(RoleEntity role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.MapId <= 0 || role.RoleId <= 0 || string.IsNullOrWhiteSpace(role.Name) || role.Level <= 0 ||
            role.MaxHealth <= 0 || role.Health < 0 || role.Health > role.MaxHealth ||
            role.MaxMana < 0 || role.Mana < 0 || role.Mana > role.MaxMana ||
            role.Experience < 0 || role.NextExperience <= 0 ||
            !float.IsFinite(role.X) || !float.IsFinite(role.Y) || !Enum.IsDefined(role.Gender) ||
            role.HeadId < 1 || role.HeadId > 74 || role.BagExtraRows < 0 || role.NextInstanceId <= 0)
            throw new InvalidDataException("角色存档包含无效字段");
    }
}
