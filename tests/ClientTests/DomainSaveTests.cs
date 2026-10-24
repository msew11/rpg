using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/mock-server 与 role-bag 的分域存档、旧格式迁移和失败恢复。</summary>
public sealed class DomainSaveTests : IDisposable
{
    /// <summary>每个验收独占的领域文件目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-domain-" + Guid.NewGuid());
    /// <summary>成功提交的目标日志，用于排除临时文件与失败提交。</summary>
    private readonly List<string> _writes = [];
    /// <summary>带提交日志的真实仓库。</summary>
    private RepoServerRole Repo => new(_directory, (path, _) => _writes.Add(Path.GetFileName(path)));

    /// <summary>创建两份精确命名领域，增加大量物品也不会扩大角色文件。</summary>
    [Fact]
    public void CreatesIndependentDomainsAndKeepsRoleFileSmall()
    {
        var repo = Repo;
        var role = repo.Create("分域角色", ServerRoleGender.Male, 2);
        Assert.Equal(new[] { "1003.bag.json", "1003.role.json" }, _writes);
        Assert.Equal(new[] { "1003.bag.json", "1003.role.json" }, Directory.GetFiles(_directory)
            .Select(Path.GetFileName).OrderBy(name => name));
        var roleBytes = File.ReadAllBytes(repo.GetRolePath(role.RoleId));
        var json = JsonNode.Parse(roleBytes)!.AsObject();
        Assert.False(json.ContainsKey("Bag"));
        Assert.False(json.ContainsKey("BagExtraRows"));
        var update = role with { BagExtraRows = 1000, Bag = new DataServerBag { Revision = 7,
            NextInstanceId = 1001, Items = Enumerable.Range(1, 1000)
                .Select(id => new DataServerItem(id, 1012, id - 1, 1)).ToArray() } };
        _writes.Clear();
        repo.SaveBag(update);
        Assert.Equal(new[] { "1003.bag.json" }, _writes);
        Assert.Equal(roleBytes, File.ReadAllBytes(repo.GetRolePath(role.RoleId)));
        Assert.True(new FileInfo(repo.GetBagPath(role.RoleId)).Length > roleBytes.Length * 10);
        Assert.Equal(update, new RepoServerRole(_directory).Load(role.RoleId));
    }

    /// <summary>另一领域独占锁定仍可保存，陈旧的聚合字段不会覆盖已提交领域。</summary>
    [Fact]
    public void SingleDomainSavesDoNotReadOrOverwriteOtherDomain()
    {
        var repo = Repo;
        var role = repo.Create("写入隔离", ServerRoleGender.Male, 1);
        var withBag = role with { BagExtraRows = 1, Bag = new DataServerBag { Revision = 1,
            NextInstanceId = 2, Items = [new DataServerItem(1, 1012, 0, 1)] } };
        using (var locked = new FileStream(repo.GetRolePath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.None))
            repo.SaveBag(withBag);
        using (var locked = new FileStream(repo.GetBagPath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.None))
            repo.SaveRole(role with { X = role.X + 10, Revision = 1 });
        var actual = repo.Load(role.RoleId);
        Assert.Equal(withBag.Bag, actual.Bag);
        Assert.Equal(1, actual.BagExtraRows);
        Assert.Equal(role.X + 10, actual.X);
        repo.SaveBag(withBag with { Bag = withBag.Bag with { Revision = 2 } });
        Assert.Equal(actual.X, repo.Load(role.RoleId).X);
    }

    /// <summary>旧存档只读不迁移，首次保存迁移完整数据并保留备份，墓碑不被旧文件复活。</summary>
    [Fact]
    public void MigratesLegacyOnSaveAndNeverRevivesLegacyTombstone()
    {
        Directory.CreateDirectory(_directory);
        var repo = Repo;
        var old = new DataServerRole { RoleId = 1003, Name = "旧角色", Level = 1, Health = 10,
            MaxHealth = 10, NextExperience = 100, BagExtraRows = 3, Bag = new DataServerBag {
                Revision = 9, NextInstanceId = 2, Items = [new DataServerItem(1, 1012, 0, 1)] } };
        var legacy = JsonSerializer.Serialize(old);
        File.WriteAllText(repo.GetLegacyPath(old.RoleId), legacy);
        Assert.Equal(old, repo.Load(old.RoleId));
        Assert.Single(repo.List());
        Assert.Empty(_writes);
        Assert.False(File.Exists(repo.GetRolePath(old.RoleId)));
        repo.SaveRole(old with { X = 30 });
        Assert.Equal(old with { X = 30 }, repo.Load(old.RoleId));
        Assert.Equal(legacy, File.ReadAllText(repo.GetLegacyPath(old.RoleId)));
        Assert.Single(repo.List());
        _writes.Clear();
        repo.Delete(old.RoleId);
        Assert.Equal(new[] { "1003.role.json" }, _writes);
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.Throws<InvalidDataException>(() => repo.Load(old.RoleId));
        Assert.True(repo.Create("后续角色", ServerRoleGender.Female, 1).RoleId > old.RoleId);
    }

    /// <summary>分域缺失、非法 JSON、错配身份或必需字段缺失均报错，不回退旧备份。</summary>
    [Theory]
    [InlineData("missing-role")]
    [InlineData("json-role")]
    [InlineData("missing")]
    [InlineData("json")]
    [InlineData("identity")]
    [InlineData("field")]
    public void CorruptSplitBagDoesNotFallbackToLegacy(string kind)
    {
        var repo = Repo;
        var role = repo.Create("拒绝损坏", ServerRoleGender.Male, 1);
        File.WriteAllText(repo.GetLegacyPath(role.RoleId), JsonSerializer.Serialize(role));
        var path = kind.EndsWith("-role", StringComparison.Ordinal) ? repo.GetRolePath(role.RoleId) : repo.GetBagPath(role.RoleId);
        if (kind is "missing" or "missing-role") File.Delete(path);
        else if (kind is "json" or "json-role") File.WriteAllText(path, "{损坏");
        else
        {
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (kind == "identity") json["RoleId"] = role.RoleId + 1;
            else json.Remove("Bag");
            File.WriteAllText(path, json.ToJsonString());
        }
        _writes.Clear();
        var error = Assert.Throws<InvalidDataException>(() => repo.Load(role.RoleId));
        Assert.Contains(Path.GetFileName(path), error.Message);
        Assert.Throws<InvalidDataException>(() => repo.List());
        Assert.Empty(_writes);
    }

    /// <summary>创建在第二个领域失败时回滚首个文件，不记录 IO，解除阻塞后可按原编号重试。</summary>
    [Fact]
    public void FailedCreateRollsBackBothDomains()
    {
        var repo = Repo;
        Directory.CreateDirectory(repo.GetRolePath(1003));
        Assert.Throws<IOException>(() => repo.Create("创建失败", ServerRoleGender.Male, 1));
        Assert.Empty(_writes);
        Assert.Empty(Directory.GetFiles(_directory));
        Directory.Delete(repo.GetRolePath(1003));
        Assert.Equal(1003, repo.Create("创建重试", ServerRoleGender.Male, 1).RoleId);
    }

    /// <summary>共同变更时第二文件锁定，首个领域回滚且无成功日志或事务残留。</summary>
    [Fact]
    public void FailedMultiDomainUpdateRestoresOriginalBytes()
    {
        var repo = Repo;
        var role = repo.Create("事务失败", ServerRoleGender.Male, 1);
        var roleBytes = File.ReadAllBytes(repo.GetRolePath(role.RoleId));
        var bagBytes = File.ReadAllBytes(repo.GetBagPath(role.RoleId));
        _writes.Clear();
        using (var locked = new FileStream(repo.GetRolePath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => repo.Save(role with { X = 100, BagExtraRows = 2 }));
        Assert.Equal(roleBytes, File.ReadAllBytes(repo.GetRolePath(role.RoleId)));
        Assert.Equal(bagBytes, File.ReadAllBytes(repo.GetBagPath(role.RoleId)));
        Assert.Empty(_writes);
        Assert.Equal(2, Directory.GetFiles(_directory).Length);
        repo.Save(role with { X = 100, BagExtraRows = 2 });
        Assert.Equal(100, repo.Load(role.RoleId).X);
        Assert.Equal(2, repo.Load(role.RoleId).BagExtraRows);
    }

    /// <summary>模拟更新中途退出，重建仓库后在读取前恢复原有领域组。</summary>
    [Fact]
    public void InterruptedUpdateIsRecoveredBeforeRead()
    {
        var repo = Repo;
        var role = repo.Create("中断恢复", ServerRoleGender.Male, 1);
        var originals = new Dictionary<string, string?> { ["role"] = File.ReadAllText(repo.GetRolePath(role.RoleId)),
            ["bag"] = File.ReadAllText(repo.GetBagPath(role.RoleId)) };
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), JsonSerializer.Serialize(originals));
        File.WriteAllText(repo.GetBagPath(role.RoleId), JsonSerializer.Serialize(
            DataServerBagFile.FromRole(role with { BagExtraRows = 8 })));
        _writes.Clear();
        Assert.Equal(role, new RepoServerRole(_directory).Load(role.RoleId));
        Assert.Equal(originals["bag"], File.ReadAllText(repo.GetBagPath(role.RoleId)));
        Assert.False(File.Exists(Path.Combine(_directory, "1003.pending.json")));
        Assert.Empty(_writes);
    }

    /// <summary>中断创建无原文件，登录枚举先回滚半份角色，不生成摘要或消耗编号。</summary>
    [Fact]
    public void InterruptedCreateIsRecoveredBeforeListing()
    {
        var repo = Repo;
        var role = repo.Create("半份角色", ServerRoleGender.Male, 1);
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), JsonSerializer.Serialize(
            new Dictionary<string, string?> { ["role"] = null, ["bag"] = null }));
        Assert.Empty(new RepoServerRole(_directory).List());
        Assert.Empty(Directory.GetFiles(_directory));
        Assert.Equal(role.RoleId, repo.Create("重试角色", ServerRoleGender.Male, 1).RoleId);
    }

    /// <summary>损坏恢复记录不得写出任意路径，也不得暴露半提交的存档。</summary>
    [Fact]
    public void InvalidRecoveryRecordIsRejected()
    {
        var repo = Repo;
        var role = repo.Create("错误恢复", ServerRoleGender.Male, 1);
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), "{\"../outside\":\"内容\"}");
        Assert.Throws<IOException>(() => repo.Load(role.RoleId));
        Assert.Throws<IOException>(() => repo.List());
        Assert.True(File.Exists(repo.GetRolePath(role.RoleId)));
    }

    /// <summary>删除本次测试独占的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
