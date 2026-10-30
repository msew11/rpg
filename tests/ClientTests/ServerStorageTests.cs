using System.Collections.Immutable;
using System.Text.Json;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.Role;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-storage 的格式、无业务校验、修改基线和中断恢复。</summary>
public sealed class ServerStorageTests : IDisposable
{
    /// <summary>每例独立的系统临时目录，不触碰实际存档。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-storage-" + Guid.NewGuid());

    /// <summary>验证 game-protocol：提升已有存档后，三表、摘要和地图文件保持原始字节且重启可读取。</summary>
    [Fact]
    public async Task PromotedStorageKeepsExistingTablesAndMapFiles()
    {
        var previous = Path.Combine(_directory, "v2");
        await new RepoRole(previous).SaveAsync(10, Sample(), DataRoleTables.All);
        await new RepoAccountRoleSummary(previous).SaveAsync("demo1", [new(10, "原角色", 1, RoleGender.Male, 1, [])]);
        Directory.CreateDirectory(Path.Combine(previous, "maps"));
        await File.WriteAllTextAsync(Path.Combine(previous, "maps", "existing.json"), "地图原始字节");
        var bytes = Directory.GetFiles(previous, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(previous, path), File.ReadAllBytes);
        Assert.Equal(Path.GetFullPath(_directory), Rpg.Server.Hosting.MockServer.PrepareStorageDirectory(_directory));
        Assert.False(Directory.Exists(previous));
        foreach (var (path, content) in bytes) Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(_directory, path)));
        Assert.Equal(Sample().Role, (await new RepoRole(_directory).LoadAsync(10)).Role);
        Assert.Single(await new RepoAccountRoleSummary(_directory).LoadAsync("demo1"));
        Rpg.Server.Hosting.MockServer.PrepareStorageDirectory(_directory);
        Assert.False(Directory.Exists(previous));
    }

    /// <summary>验证 game-protocol：正式目录已有同名文件时禁止覆盖，保留两份文件供处理。</summary>
    [Fact]
    public void PromotedStorageDoesNotOverwriteExistingTarget()
    {
        var previous = Path.Combine(_directory, "v2");
        Directory.CreateDirectory(previous);
        File.WriteAllText(Path.Combine(previous, "data.json"), "原子目录数据");
        File.WriteAllText(Path.Combine(_directory, "data.json"), "正式目录数据");
        Assert.Throws<IOException>(() => Rpg.Server.Hosting.MockServer.PrepareStorageDirectory(_directory));
        Assert.Equal("原子目录数据", File.ReadAllText(Path.Combine(previous, "data.json")));
        Assert.Equal("正式目录数据", File.ReadAllText(Path.Combine(_directory, "data.json")));
    }
    /// <summary>验证 server-account 入口拒绝契约：空角色快照及空账号在文件操作前失败。</summary>
    [Fact]
    public async Task NullSnapshotsAreRejectedBeforePendingRecoveryOrWrites()
    {
        Directory.CreateDirectory(_directory);
        var pending = PathOf("pending");
        await File.WriteAllTextAsync(pending, "未完成且无法解析的回滚记录");
        var roles = new RepoRole(_directory);
        var accounts = new RepoAccountRoleSummary(_directory);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await roles.SaveAsync(10, null!, DataRoleTables.All));
        Assert.Throws<ArgumentNullException>(() => accounts.SaveAsync(null!, []));
        Assert.Equal("未完成且无法解析的回滚记录", await File.ReadAllTextAsync(pending));
        Assert.Single(Directory.GetFiles(_directory));
    }
    /// <summary>验证 server-world-entry：男女首次出生点都裁剪在小地图最后一个逻辑像素。</summary>
    [Theory]
    [InlineData(Rpg.Protocol.RoleGenderPb.Male)]
    [InlineData(Rpg.Protocol.RoleGenderPb.Female)]
    public void InitialRoleClipsSpawnToMapBounds(Rpg.Protocol.RoleGenderPb gender)
    {
        var role = Rpg.Server.Modules.Role.Cache.DataCacheRole.CreateInitial(new() { RoleId = 1003,
            Name = "边缘角色", Level = 1, Gender = gender, HeadId = 17 }, new() { Id = 9, Width = 2, Height = 3, TileSize = 32 });
        Assert.Equal(9, role.MapId); Assert.Equal(63, role.X); Assert.Equal(95, role.Y);
    }

    /// <summary>摘要缺失不写盘，保存数组并使用规范账号名。</summary>
    [Fact]
    public async Task MissingAccountStaysInMemoryAndSavedArrayUsesNormalizedName()
    {
        var repo = new RepoAccountRoleSummary(_directory);
        Assert.Empty(await repo.LoadAsync(" Alice "));
        Assert.False(Directory.Exists(_directory));
        ImmutableArray<DataRoleSummary> data = [new(8, "角色", -2, RoleGender.Unspecified, 0, [999])];
        await repo.SaveAsync(" ALICE ", data);
        var path = Path.Combine(_directory, "alice.role_summary.json");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(8, json.RootElement[0].GetProperty("roleId").GetInt64());
        Assert.Equal(0, json.RootElement[0].GetProperty("gender").GetInt32());
        Assert.Equal(data[0].Name, (await repo.LoadAsync("alice"))[0].Name);
        Assert.Single(Directory.GetFiles(_directory));
    }
    /// <summary>验证 server-account 的路径安全契约：非法账号在读写前拒绝，不创建目录。</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("CON")]
    [InlineData("name.")]
    public async Task UnsafeAccountKeysFailBeforeAnyFileOperation(string username)
    {
        var repo = new RepoAccountRoleSummary(_directory);
        await Assert.ThrowsAsync<ArgumentException>(async () => await repo.LoadAsync(username));
        Assert.Throws<ArgumentException>(() => repo.SaveAsync(username, []));
        Assert.False(Directory.Exists(_directory));
    }
    /// <summary>验证 server-storage 的空表格式：默认不可变数组保存为 [] 并可重新加载。</summary>
    [Fact]
    public async Task DefaultAccountArrayIsSavedAsEmptyJsonArray()
    {
        var repo = new RepoAccountRoleSummary(_directory);
        await repo.SaveAsync(" ALICE ", default);
        Assert.Equal("[]", await File.ReadAllTextAsync(Path.Combine(_directory, "alice.role_summary.json")));
        Assert.Empty(await repo.LoadAsync("alice"));
    }
    /// <summary>三表按角色加载，不做身份、重复实例、数值或关联校验。</summary>
    [Fact]
    public async Task RoleRoundTripKeepsUnvalidatedDataAndOnlyWritesSelectedTables()
    {
        var repo = new RepoRole(_directory);
        var original = Sample() with
        {
            Role = new DataRole { RoleId = 999, Level = -1, IsDeleted = true },
            Items = [new(1, -5, -2, 0), new(1, 0, 0, -1)], Equipments = [new() { InstanceId = 200 }]
        };
        await repo.SaveAsync(10, original, DataRoleTables.All);
        var loaded = await repo.LoadAsync(10);
        Assert.Equal(original.Role, loaded.Role);
        Assert.Equal(original.Items.ToArray(), loaded.Items.ToArray());
        Assert.Equal(original.Equipments.ToArray(), loaded.Equipments.ToArray());
        var itemBytes = await File.ReadAllBytesAsync(PathOf("item"));
        var equipmentBytes = await File.ReadAllBytesAsync(PathOf("equipment"));
        await repo.SaveAsync(10, loaded with { Role = loaded.Role with { Name = "更新" }, Items = [] }, DataRoleTables.Role);
        Assert.Equal("更新", (await repo.LoadAsync(10)).Role.Name);
        Assert.Equal(itemBytes, await File.ReadAllBytesAsync(PathOf("item")));
        Assert.Equal(equipmentBytes, await File.ReadAllBytesAsync(PathOf("equipment")));
        Assert.False(File.Exists(PathOf("pending")));
    }
    /// <summary>三张文件均必需，空数组可明确表达空表。</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("item")]
    [InlineData("equipment")]
    public async Task MissingRequiredRoleTableFails(string table)
    {
        var repo = new RepoRole(_directory);
        await repo.SaveAsync(10, Sample(), DataRoleTables.All);
        Assert.Empty((await repo.LoadAsync(10)).Items);
        File.Delete(PathOf(table));
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await repo.LoadAsync(10));
    }
    /// <summary>拒绝不能反序列化的根，而不增加业务字段校验。</summary>
    [Theory]
    [InlineData("role", "null")]
    [InlineData("item", "null")]
    [InlineData("equipment", "null")]
    [InlineData("role", "{")]
    [InlineData("item", "{}")]
    [InlineData("equipment", "[false]")]
    public async Task InvalidRoleJsonFailsWithoutChangingSource(string table, string text)
    {
        var repo = new RepoRole(_directory);
        await repo.SaveAsync(10, Sample(), DataRoleTables.All);
        await File.WriteAllTextAsync(PathOf(table), text);
        await Assert.ThrowsAnyAsync<JsonException>(async () => await repo.LoadAsync(10));
        Assert.Equal(text, await File.ReadAllTextAsync(PathOf(table)));
    }
    /// <summary>摘要损坏不能被误判成新账号。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[")]
    public async Task InvalidSummaryJsonDoesNotBecomeEmptyAccount(string text)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "alice.role_summary.json");
        await File.WriteAllTextAsync(path, text);
        await Assert.ThrowsAnyAsync<JsonException>(async () => await new RepoAccountRoleSummary(_directory).LoadAsync("alice"));
        Assert.Equal(text, await File.ReadAllTextAsync(path));
    }
    /// <summary>分别在各表替换后模拟中断，重载必须恢复完整旧数据，再次保存可成功。</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("item")]
    [InlineData("equipment")]
    public async Task InterruptedMultiTableCommitRestoresOriginalBytesBeforeReadAndRetry(string interruption)
    {
        var repo = new RepoRole(_directory);
        var original = Sample();
        await repo.SaveAsync(10, original, DataRoleTables.All);
        var originalBytes = new[] { "role", "item", "equipment" }.ToDictionary(table => table, table => File.ReadAllBytes(PathOf(table)));
        var updated = original with { Role = original.Role with { Name = "新" }, Items = [new(1, 4, 0, 2)], Equipments = [new() { InstanceId = 1 }] };
        var interrupted = new RepoRole(_directory) { AfterTableWritten = table => { if (table == interruption) throw new IOException("模拟进程中断"); } };
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.SaveAsync(10, updated, DataRoleTables.All));
        Assert.True(File.Exists(PathOf("pending")));
        Assert.Equal(original.Role, (await repo.LoadAsync(10)).Role);
        foreach (var (table, bytes) in originalBytes) Assert.Equal(bytes, await File.ReadAllBytesAsync(PathOf(table)));
        Assert.False(File.Exists(PathOf("pending")));
        await repo.SaveAsync(10, updated, DataRoleTables.All);
        Assert.Equal(updated.Role, (await repo.LoadAsync(10)).Role);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }
    /// <summary>恢复失败保留记录，非法表名也不得产生任意路径写入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryFailurePreservesJournalAndBlocksFurtherWrites(bool unsafeTable)
    {
        var repo = new RepoRole(_directory);
        await repo.SaveAsync(10, Sample(), DataRoleTables.All);
        var table = unsafeTable ? "../outside" : "role";
        await File.WriteAllTextAsync(PathOf("pending"), JsonSerializer.Serialize(new Dictionary<string, byte[]?>
        { [table] = [1, 2, 3], ["item"] = [] }));
        if (!unsafeTable) { File.Delete(PathOf("role")); Directory.CreateDirectory(PathOf("role")); }
        await Assert.ThrowsAnyAsync<Exception>(async () => await repo.LoadAsync(10));
        await Assert.ThrowsAnyAsync<Exception>(async () => await repo.SaveAsync(10, Sample(), DataRoleTables.All));
        Assert.True(File.Exists(PathOf("pending")));
        Assert.False(File.Exists(Path.Combine(_directory, "outside")));
    }
    /// <summary>修改标记按保存基线及嵌套数组内容计算，恢复原值取消待写。</summary>
    [Fact]
    [Trait("Runtime", "Godot")]
    public async Task ContextTracksTablesAndDeepSummaryContentAgainstSavedBaseline()
    {
        using var env = ConfigTestFixture.CreateEnv();
        using var config = new Rpg.Config.ConfigService(env);
        var repo = new DoubleServerRepo();
        await using var locator = new Rpg.Server.Modules.ServerModules(config, repo, repo, repo, _ => new());
        var original = Sample();
        var role = new RoleContext(config, locator, 10, original);
        Assert.Throws<ArgumentNullException>(() => role.Cache.Commit(null!));
        Assert.Same(original.Role, role.Cache.Role.Value);
        role.Cache.Commit(original with { Role = original.Role with { Name = "甲" }, Items = [new(1, 3, 0, 1)] });
        Assert.Equal(DataRoleTables.Role | DataRoleTables.Item, role.Cache.DirtyTables);
        role.Cache.Commit(original with { Role = original.Role with { }, Items = ImmutableArray.Create<DataItem>() });
        Assert.Equal(DataRoleTables.None, role.Cache.DirtyTables);
        role.Cache.Commit(original with { Equipments = [new() { InstanceId = 1 }] });
        Assert.Equal(DataRoleTables.Equipment, role.Cache.DirtyTables);
        role.Cache.AcceptSaved();
        Assert.Equal(DataRoleTables.None, role.Cache.DirtyTables);
        var summary = new DataRoleSummary(1, "原", 1, RoleGender.Male, 1, [1, 2]);
        var account = new LoginContext(config, locator, "alice", new([summary]));
        var loginSnapshot = account.Cache.Snapshot();
        Assert.Throws<ArgumentNullException>(() => account.Cache.Commit(null!));
        Assert.Equal(loginSnapshot.Summaries, account.Cache.Summaries.Value);
        account.Cache.Commit(loginSnapshot with { Summaries = [summary with { EquippedItems = [1, 2] }] });
        Assert.False(account.Cache.SummariesDirty);
        account.Cache.Commit(loginSnapshot with { Summaries = [summary with { Name = "新" }] });
        Assert.True(account.Cache.SummariesDirty);
        Assert.Equal("新", account.Cache.Snapshot().Summaries[0].Name);
        Assert.Equal("原", loginSnapshot.Summaries[0].Name);
        account.Cache.Commit(loginSnapshot);
        Assert.False(account.Cache.SummariesDirty);
        var updated = loginSnapshot with { Summaries = [summary with { Name = "保存后" }] };
        account.Cache.Commit(updated);
        account.Cache.AcceptSaved();
        Assert.False(account.Cache.SummariesDirty);
        account.Cache.Commit(loginSnapshot);
        Assert.True(account.Cache.SummariesDirty);
        account.Cache.Commit(updated);
        Assert.False(account.Cache.SummariesDirty);
    }
    /// <summary>验证 server-storage 的内容基线契约：默认数组与空表等价，恢复后取消待写。</summary>
    [Fact]
    [Trait("Runtime", "Godot")]
    public async Task LoginDefaultSummaryArrayBehavesAsEmptyTable()
    {
        using var env = ConfigTestFixture.CreateEnv();
        using var config = new Rpg.Config.ConfigService(env);
        var repo = new DoubleServerRepo();
        await using var locator = new Rpg.Server.Modules.ServerModules(config, repo, repo, repo, _ => new());
        var account = new LoginContext(config, locator, "alice", new(default));
        Assert.Empty(account.Cache.Snapshot().Summaries);
        account.Cache.Commit(new([]));
        Assert.False(account.Cache.SummariesDirty);
        account.Cache.Commit(new([new(1, "角色", 1, RoleGender.Male, 1, [])]));
        Assert.True(account.Cache.SummariesDirty);
        account.Cache.Commit(new(default));
        Assert.Empty(account.Cache.Summaries.Value);
        Assert.False(account.Cache.SummariesDirty);
    }
    /// <summary>建立不依赖配置或 Godot 的简单快照。</summary>
    internal static DataRoleSnapshot Sample() => new(new DataRole { RoleId = 10, Name = "原角色" }, [], []);
    /// <summary>隔离目录内固定角色的表路径。</summary>
    private string PathOf(string table) => Path.Combine(_directory, "10." + table + ".json");
    /// <summary>只清理本测试创建的独立临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
