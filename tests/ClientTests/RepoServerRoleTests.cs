using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/role-management 的全新空列表、显式创建及旧存档兼容场景。</summary>
public sealed class RepoServerRoleTests : IDisposable
{
    /// <summary>只访问本次测试的独立存档目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-empty-roles-" + Guid.NewGuid());

    /// <summary>全新目录重复读取与重建仓库始终为空，不写初始化标记或角色。</summary>
    [Fact]
    public void FreshStorageStaysEmptyAcrossRestart()
    {
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.False(Directory.Exists(_directory));
    }

    /// <summary>读取或删除缺失角色不生成任何存档，包括曾用于演示的编号。</summary>
    [Theory]
    [InlineData(1001)]
    [InlineData(1002)]
    [InlineData(9999)]
    public void MissingRoleIsNeverCreated(long roleId)
    {
        var repo = new RepoServerRole(_directory);
        Assert.Throws<InvalidDataException>(() => repo.Load(roleId));
        Assert.Throws<InvalidDataException>(() => repo.Delete(roleId));
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(repo.List());
    }

    /// <summary>首次创建保留既有新角色属性、性别坐标及头型，重启可读且墓碑不复用编号。</summary>
    [Theory]
    [InlineData(ServerRoleGender.Male, 16016, 16016)]
    [InlineData(ServerRoleGender.Female, 16144, 16080)]
    public void FirstCreatedRolePersistsWithoutDemoRoles(ServerRoleGender gender, int x, int y)
    {
        var repo = new RepoServerRole(_directory);
        var created = repo.Create("  首个角色  ", gender, 74);
        Assert.Equal(1003, created.RoleId);
        Assert.Equal("首个角色", created.Name);
        Assert.Equal(gender, created.Gender);
        Assert.Equal(74, created.HeadId);
        Assert.Equal(1, created.Level);
        Assert.Equal(100, created.Health);
        Assert.Equal(100, created.MaxHealth);
        Assert.Equal(60, created.Mana);
        Assert.Equal(60, created.MaxMana);
        Assert.Equal(0, created.Experience);
        Assert.Equal(500, created.NextExperience);
        Assert.Equal(x, created.X);
        Assert.Equal(y, created.Y);
        var restarted = new RepoServerRole(_directory);
        Assert.Equal(created, Assert.Single(restarted.List()));
        Assert.Equal(created, restarted.Load(created.RoleId));
        restarted.Delete(created.RoleId);
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.True(new RepoServerRole(_directory).Create("下个角色", gender, 1).RoleId > created.RoleId);
    }

    /// <summary>有无旧标记都保留已有角色与墓碑，缺失角色不会被补建。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingStorageIsPreservedWithoutBackfill(bool hasLegacyMarker)
    {
        Rpg.Tests.RoleTestData.Seed(_directory);
        var repo = new RepoServerRole(_directory);
        repo.Delete(1002);
        var existing = File.ReadAllText(repo.GetRolePath(1001));
        var tombstone = File.ReadAllText(repo.GetRolePath(1002));
        if (hasLegacyMarker) File.WriteAllText(Path.Combine(_directory, "initialized"), "1");
        Assert.Equal(1001, Assert.Single(new RepoServerRole(_directory).List()).RoleId);
        Assert.Equal(existing, File.ReadAllText(repo.GetRolePath(1001)));
        Assert.Equal(tombstone, File.ReadAllText(repo.GetRolePath(1002)));
        foreach (var file in new[] { repo.GetRolePath(1001), repo.GetItemPath(1001), repo.GetEquipmentPath(1001), repo.GetSummaryPath(1001) }) File.Delete(file);
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.False(File.Exists(repo.GetRolePath(1001)));
        Assert.Equal(hasLegacyMarker, File.Exists(Path.Combine(_directory, "initialized")));
    }

    /// <summary>清理本次测试创建的隔离存档。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
