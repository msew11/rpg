using System;
using System.Linq;
using Rpg.Server.Entity;
using Rpg.Server.Context;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.Login;

/// <summary>角色创建的内存候选工厂，仓库与请求处理共用。</summary>
internal static class RoleCreation
{
    /// <summary>校验列表唯一名称和外观，准备初始实体，不消耗失败编号。</summary>
    internal static ServerRoleCache Create(LoginContext cache, string name, ServerRoleGender gender,
        int headId, int mapId, float? spawnX = null, float? spawnY = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 16 || name.Any(char.IsControl))
            throw new ArgumentException("角色名字需为 1 至 16 个字符，且不能包含控制字符");
        if (cache.EntityCache.Summaries.Values.Any(role => !role.IsDeleted && string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("角色名字已存在");
        if (!Enum.IsDefined(gender) || headId is < 1 or > 74) throw new ArgumentException("性别或头型无效");
        var id = checked(cache.EntityCache.Summaries.Keys.DefaultIfEmpty(1002).Max() + 1);
        return new ServerRoleCache(new RoleEntity
        {
            RoleId = id, MapId = mapId, Name = name, Gender = gender, HeadId = headId, Level = 1,
            Health = 100, MaxHealth = 100, Mana = 60, MaxMana = 60, NextExperience = 500,
            X = spawnX ?? (gender == ServerRoleGender.Male ? 16016 : 16144),
            Y = spawnY ?? (gender == ServerRoleGender.Male ? 16016 : 16080)
        });
    }
}
