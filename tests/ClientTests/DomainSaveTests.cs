using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 mock-server 的单例/多例表、现行格式校验和四表事务恢复。</summary>
public sealed class DomainSaveTests : IDisposable
{
    /// <summary>独占实体表目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-domain-" + Guid.NewGuid());
    /// <summary>只有成功组提交才发布的目标日志。</summary>
    private readonly List<string> _writes = [];
    /// <summary>真实磁盘仓库。</summary>
    private RepoServerRole Repo => new(_directory, (path, _) => _writes.Add(Path.GetFileName(path)));
    /// <summary>新角色必须共同拥有的四张表。</summary>
    private static readonly string[] Domains = ["item", "equipment", "role", "role_summary"];

    /// <summary>mock-server：role 是单对象、实例表是数组，网络版本永不落地。</summary>
    [Fact]
    public void CreatesIndependentDomainsAndKeepsRoleFileSmall()
    {
        var repo = Repo;
        var role = repo.Create("分域角色", ServerRoleGender.Male, 2);
        Assert.Equal(Domains.Select(domain => $"1003.{domain}.json"), _writes);
        Assert.Equal(4, Directory.GetFiles(_directory).Length);
        Assert.False(File.Exists(Path.Combine(_directory, $"{role.RoleId}.bag.json")));
        var update = role with { Revision = 9, AcknowledgedAction = 3, BagExtraRows = 1000,
            Bag = new DataServerBag { Revision = 7, EquipmentRevision = 4, NextInstanceId = 1001,
                Items = Enumerable.Range(1, 1000).Select(id => new DataServerItem(id, 1012, id - 1, 1)).ToArray() } };
        _writes.Clear();
        repo.SaveBag(update);
        Assert.DoesNotContain("1003.role_summary.json", _writes);
        using var scalar = JsonDocument.Parse(File.ReadAllText(repo.GetRolePath(role.RoleId)));
        Assert.Equal(JsonValueKind.Object, scalar.RootElement.ValueKind);
        Assert.Equal(1001, scalar.RootElement.GetProperty("nextInstanceId").GetInt64());
        Assert.False(scalar.RootElement.TryGetProperty("bag", out _));
        foreach (var domain in Domains)
            Assert.DoesNotContain("revision", File.ReadAllText(Path.Combine(_directory, $"1003.{domain}.json")), StringComparison.OrdinalIgnoreCase);
        using var items = JsonDocument.Parse(File.ReadAllText(repo.GetItemPath(role.RoleId)));
        Assert.Equal(JsonValueKind.Array, items.RootElement.ValueKind);
        Assert.Equal(1000, items.RootElement.GetArrayLength());
        Assert.True(new FileInfo(repo.GetItemPath(role.RoleId)).Length > new FileInfo(repo.GetRolePath(role.RoleId)).Length * 10);
        var actual = repo.Load(role.RoleId);
        Assert.Equal(update.Bag.Items, actual.Bag.Items);
        Assert.Equal((0L, 0L, 0L), (actual.Revision, actual.Bag.Revision, actual.Bag.EquipmentRevision));
        Assert.Equal(0, actual.AcknowledgedAction);
    }

    /// <summary>mock-server：角色移动不读取物品表，旧角色候选不覆盖实例编号或扩容。</summary>
    [Fact]
    public void SingleDomainSavesDoNotReadOrOverwriteOtherDomain()
    {
        var repo = Repo;
        var role = repo.Create("写入隔离", ServerRoleGender.Male, 1);
        var withBag = role with { BagExtraRows = 1, Bag = new DataServerBag {
            NextInstanceId = 2, Items = [new DataServerItem(1, 1012, 0, 1)] } };
        repo.SaveBag(withBag);
        var bytes = File.ReadAllBytes(repo.GetItemPath(role.RoleId));
        using (var locked = new FileStream(repo.GetItemPath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.None))
            repo.SaveRole(role with { X = role.X + 10 });
        Assert.Equal(bytes, File.ReadAllBytes(repo.GetItemPath(role.RoleId)));
        var actual = repo.Load(role.RoleId);
        Assert.Equal(withBag.Bag, actual.Bag);
        Assert.Equal(1, actual.BagExtraRows);
        Assert.Equal(role.X + 10, actual.X);
        _writes.Clear();
        repo.SaveBag(withBag with { Bag = withBag.Bag with { Items = [new(1, 1012, 3, 1)] } });
        Assert.Equal(new[] { "1003.item.json" }, _writes);
        Assert.Equal(actual.X, repo.Load(role.RoleId).X);
    }

    /// <summary>mock-server：当前四表删除保留墓碑，重启后角色不复活且编号不复用。</summary>
    [Fact]
    public void CurrentTombstonesDoNotReviveOrReuseRoleIds()
    {
        var repo = Repo;
        var role = repo.Create("删除角色", ServerRoleGender.Male, 1);
        repo.SaveBag(role with { Bag = new DataServerBag {
            NextInstanceId = 2, Items = [new(1, 1012, 0, 1)] } });
        _writes.Clear();
        repo.Delete(role.RoleId);
        Assert.Equal(new[] { "1003.role.json", "1003.role_summary.json" }, _writes);
        var restarted = new RepoServerRole(_directory);
        Assert.Empty(restarted.ListSummaries());
        Assert.Throws<InvalidDataException>(() => restarted.Load(role.RoleId));
        Assert.True(restarted.Create("后续角色", ServerRoleGender.Female, 1).RoleId > role.RoleId);
    }
    /// <summary>mock-server：当前表缺失、损坏或身份不符时拒绝读取，不改写任何表。</summary>
    [Theory]
    [InlineData("missing-role")]
    [InlineData("json-role")]
    [InlineData("missing")]
    [InlineData("json")]
    [InlineData("identity")]
    [InlineData("field")]
    public void CorruptEntityTablesFailWithoutWritingFiles(string kind)
    {
        var repo = Repo;
        var role = repo.Create("拒绝损坏", ServerRoleGender.Male, 1);
        var path = kind.EndsWith("-role", StringComparison.Ordinal) || kind == "identity"
            ? repo.GetRolePath(role.RoleId) : repo.GetItemPath(role.RoleId);
        if (kind is "missing" or "missing-role") File.Delete(path);
        else if (kind is "json" or "json-role") File.WriteAllText(path, "{损坏");
        else if (kind == "identity")
        {
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            json["roleId"] = role.RoleId + 1;
            File.WriteAllText(path, json.ToJsonString());
        }
        else File.WriteAllText(path, "[{\"instanceId\":1,\"itemId\":1012}]");
        _writes.Clear();
        Assert.Single(repo.ListSummaries());
        var error = Assert.Throws<InvalidDataException>(() => repo.Load(role.RoleId));
        Assert.Contains(Path.GetFileName(path), error.Message);
        Assert.Empty(_writes);
    }

    /// <summary>mock-server：旧聚合 JSON 不进入列表、不迁移，显式加载失败且原文件不变。</summary>
    [Fact]
    public void UnsupportedAggregateFileIsNotLoadedOrMigrated()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "1003.json");
        const string original = "{\"RoleId\":1003,\"Name\":\"旧格式\",\"Bag\":{}}";
        File.WriteAllText(path, original);
        Assert.Empty(Repo.ListSummaries());
        Assert.Throws<InvalidDataException>(() => Repo.Load(1003));
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_directory));
        Assert.Empty(_writes);
    }

    /// <summary>mock-server：摘要缺失时直接失败，不读取完整物品表或创建替代摘要。</summary>
    [Fact]
    public void MissingSummaryDoesNotReadItemsOrBackfillFiles()
    {
        var repo = Repo;
        var role = repo.Create("缺少摘要", ServerRoleGender.Male, 1);
        File.Delete(repo.GetSummaryPath(role.RoleId));
        _writes.Clear();
        using var locked = new FileStream(repo.GetItemPath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.None);
        var error = Assert.Throws<InvalidDataException>(() => repo.ListSummaries());
        Assert.Contains(repo.GetSummaryPath(role.RoleId), error.Message);
        Assert.False(File.Exists(repo.GetSummaryPath(role.RoleId)));
        Assert.Empty(_writes);
    }
    /// <summary>mock-server：创建中途失败回滚已写表，解除阻塞后仍使用原编号。</summary>
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

    /// <summary>mock-server：多表变更后段锁定时全部回滚，解锁后共同重试。</summary>
    [Fact]
    public void FailedMultiDomainUpdateRestoresOriginalBytes()
    {
        var repo = Repo;
        var role = repo.Create("事务失败", ServerRoleGender.Male, 1);
        var originals = Domains.ToDictionary(domain => domain,
            domain => File.ReadAllBytes(Path.Combine(_directory, $"1003.{domain}.json")));
        _writes.Clear();
        var candidate = role with { Name = "更新名称", X = 100, BagExtraRows = 2,
            Bag = new() { NextInstanceId = 2, Items = [new(1, 1012, 0, 1)] } };
        using (var locked = new FileStream(repo.GetSummaryPath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => repo.Save(candidate));
        foreach (var (domain, bytes) in originals)
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_directory, $"1003.{domain}.json")));
        Assert.Empty(_writes);
        Assert.Equal(4, Directory.GetFiles(_directory).Length);
        repo.Save(candidate);
        Assert.Equal(100, repo.Load(role.RoleId).X);
        Assert.Equal("更新名称", Assert.Single(repo.ListSummaries()).Name);
    }

    /// <summary>mock-server：更新中断在读取前恢复四表原始文本。</summary>
    [Fact]
    public void InterruptedUpdateIsRecoveredBeforeRead()
    {
        var repo = Repo;
        var role = repo.Create("中断恢复", ServerRoleGender.Male, 1);
        var originals = Domains.ToDictionary(domain => domain,
            domain => (string?)File.ReadAllText(Path.Combine(_directory, $"1003.{domain}.json")));
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), JsonSerializer.Serialize(originals));
        File.WriteAllText(repo.GetItemPath(role.RoleId), "损坏的半份表");
        _writes.Clear();
        Assert.Equal(role, new RepoServerRole(_directory).Load(role.RoleId));
        Assert.Equal(originals["item"], File.ReadAllText(repo.GetItemPath(role.RoleId)));
        Assert.False(File.Exists(Path.Combine(_directory, "1003.pending.json")));
        Assert.Empty(_writes);
    }

    /// <summary>mock-server：中断创建在枚举摘要前删除全部半成品，不消耗编号。</summary>
    [Fact]
    public void InterruptedCreateIsRecoveredBeforeListing()
    {
        var repo = Repo;
        var role = repo.Create("半份角色", ServerRoleGender.Male, 1);
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), JsonSerializer.Serialize(
            Domains.ToDictionary(domain => domain, _ => (string?)null)));
        Assert.Empty(new RepoServerRole(_directory).ListSummaries());
        Assert.Empty(Directory.GetFiles(_directory));
        Assert.Equal(role.RoleId, repo.Create("重试角色", ServerRoleGender.Male, 1).RoleId);
    }

    /// <summary>mock-server：非法领域回滚记录被拒绝，不能写入任意路径。</summary>
    [Fact]
    public void InvalidRecoveryRecordIsRejected()
    {
        var repo = Repo;
        var role = repo.Create("错误恢复", ServerRoleGender.Male, 1);
        File.WriteAllText(Path.Combine(_directory, "1003.pending.json"), "{\"../outside\":\"内容\"}");
        Assert.Throws<IOException>(() => repo.Load(role.RoleId));
        Assert.Throws<IOException>(() => repo.ListSummaries());
        Assert.True(File.Exists(repo.GetRolePath(role.RoleId)));
    }

    /// <summary>删除仅属于本次验收的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
