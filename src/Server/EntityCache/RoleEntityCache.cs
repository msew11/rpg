using System;
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
}
