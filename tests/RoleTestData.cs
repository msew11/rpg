using Rpg.Server;
using Rpg.Server.Data;

namespace Rpg.Tests;

/// <summary>为 client-session、mock-server 与外观验收显式准备已有角色存档。</summary>
internal static class RoleTestData
{
    /// <summary>写入两份固定旧角色数据，测试不得依赖正式服务自动生成角色。</summary>
    internal static void Seed(string directory)
    {
        var repo = new RepoServerRole(directory);
        repo.Save(new DataServerRole { RoleId = 1001, Name = "晨星", Level = 5, Health = 86, MaxHealth = 100,
            Mana = 42, MaxMana = 60, Experience = 240, NextExperience = 500, X = 16016, Y = 16016, Gender = ServerRoleGender.Male });
        repo.Save(new DataServerRole { RoleId = 1002, Name = "流光", Level = 3, Health = 64, MaxHealth = 80,
            Mana = 75, MaxMana = 90, Experience = 120, NextExperience = 300, X = 16144, Y = 16080, Gender = ServerRoleGender.Female });
    }
}
