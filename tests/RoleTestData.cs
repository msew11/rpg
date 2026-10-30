using System.Collections.Immutable;
using System.Linq;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Common.Data;
using Rpg.Config.Data;
using Rpg.Const;

namespace Rpg.Tests;

/// <summary>为 client-ui、role-equipment 的隔离验收准备正式摘要及三表。</summary>
internal static class RoleTestData
{
    /// <summary>为 demo1 账号写入两份固定角色；测试不依赖生产自动建角。</summary>
    internal static void Seed(string directory)
    {
        var repo = new RepoRole(directory);
        var summaries = ImmutableArray.Create(
            new DataRoleSummary(1001, "晨星", 5, Rpg.Server.Modules.Common.Data.RoleGender.Male, 1, []),
            new DataRoleSummary(1002, "流光", 3, Rpg.Server.Modules.Common.Data.RoleGender.Female, 1, []));
        new RepoAccountRoleSummary(directory).SaveAsync("demo1", summaries).AsTask().GetAwaiter().GetResult();
        foreach (var summary in summaries)
        {
            var role = new DataRole { RoleId = summary.RoleId, MapId = 1, HeadId = 1, Name = summary.Name,
                Level = summary.Level, Health = summary.RoleId == 1001 ? 86 : 64, MaxHealth = summary.RoleId == 1001 ? 100 : 80,
                Mana = summary.RoleId == 1001 ? 42 : 75, MaxMana = summary.RoleId == 1001 ? 60 : 90,
                Experience = summary.RoleId == 1001 ? 240 : 120, NextExperience = summary.RoleId == 1001 ? 500 : 300,
                X = summary.RoleId == 1001 ? 16016 : 16144, Y = summary.RoleId == 1001 ? 16016 : 16080, Gender = summary.Gender };
            repo.SaveAsync(role.RoleId, new(role, [], []), DataRoleTables.All).AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>在启动前准备私有实例及装备扩展，摘要同步公开穿戴。</summary>
    internal static void SetItems(string directory, long roleId, System.Collections.Generic.IEnumerable<DataItem> source, long nextInstanceId)
    {
        var items = source.ToArray();
        var repo = new RepoRole(directory);
        var before = repo.LoadAsync(roleId).AsTask().GetAwaiter().GetResult();
        var equipment = items.Select(item => new DataEquipment { InstanceId = item.InstanceId }).ToImmutableArray();
        repo.SaveAsync(roleId, new(before.Role with { NextInstanceId = nextInstanceId }, items.ToImmutableArray(), equipment),
            DataRoleTables.All).AsTask().GetAwaiter().GetResult();
        var summaries = new RepoAccountRoleSummary(directory);
        var roles = summaries.LoadAsync("demo1").AsTask().GetAwaiter().GetResult();
        summaries.SaveAsync("demo1", roles.Select(role => role.RoleId == roleId ? role with {
            EquippedItems = items.Where(item => item.Equipped).Select(item => item.ItemId).ToImmutableArray() } : role)
            .ToImmutableArray()).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>构造隔离道具配置，避免无关磁盘加载。</summary>
    internal static ItemCfg Item(int id, string name, int maxStack, ItemCategory category = default, ItemSubCategory subCategory = default) => new()
    { Id = id, Name = name, MaxStack = maxStack, Category = category, SubCategory = subCategory, RelatedId = id };
}
