using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Storage;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-storage 的地图清单提交、故障恢复与逐块文件边界。</summary>
public sealed class ServerWorldStorageTests : IDisposable
{
    /// <summary>每个用例独占的 根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-world-" + Guid.NewGuid());
    /// <summary>测试规格：每块 64 逻辑像素。</summary>
    internal static DataWorldLayout Layout => new(1, 100, 100, 32, 2);
    /// <summary>本例清单路径。</summary>
    private string Manifest => Path.Combine(_directory, "maps", "1", "manifest.json");
    /// <summary>本例块目录。</summary>
    private string Chunks => Path.Combine(_directory, "maps", "1", "chunks");
    /// <summary>没有清单时不扫描孤立块，不创建目录；无变化保存也不写盘。</summary>
    [Fact]
    public async Task MissingManifestAndNoChangesStayInMemoryOnly()
    {
        var repo = new RepoWorld(_directory);
        Assert.Empty((await repo.LoadAsync(Layout)).Objects);
        await repo.SaveAsync(Layout, new(default), ImmutableHashSet<DataWorldChunk>.Empty);
        Assert.False(Directory.Exists(_directory));
        Directory.CreateDirectory(Chunks);
        await File.WriteAllTextAsync(Path.Combine(Chunks, "0_0." + Guid.NewGuid().ToString("N") + ".json"), "broken orphan");
        Assert.Empty((await repo.LoadAsync(Layout)).Objects);
        Assert.False(File.Exists(Manifest));
    }
    /// <summary>只替换变化块，未变块文件与引用不变；跨块移动、末对象删除及清空可重启恢复。</summary>
    [Fact]
    public async Task ChangedBlocksReuseUnchangedReferencesAndEmptyMapSurvivesRestart()
    {
        var repo = new RepoWorld(_directory);
        var first = Object(1);
        var second = Object(70);
        await repo.SaveAsync(Layout, new([first, second]), Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0)));
        var unchanged = Directory.GetFiles(Chunks, "1_0.*.json").Single();
        var bytes = await File.ReadAllBytesAsync(unchanged);
        File.SetLastWriteTimeUtc(unchanged, new DateTime(2000, 1, 1));
        var changed = first with { Count = 3, Revision = 2 };
        await repo.SaveAsync(Layout, new([changed, second]), Dirty(new DataWorldChunk(0, 0)));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(unchanged));
        Assert.Equal(2000, File.GetLastWriteTimeUtc(unchanged).Year);
        Assert.Equal(2, Directory.GetFiles(Chunks).Length);
        AssertObjects([changed, second], await new RepoWorld(_directory).LoadAsync(Layout));
        var moved = changed with { X = 140 };
        await repo.SaveAsync(Layout, new([moved, second]), Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(2, 0)));
        Assert.Empty(Directory.GetFiles(Chunks, "0_0.*.json"));
        AssertObjects([moved, second], await repo.LoadAsync(Layout));
        await repo.SaveAsync(Layout, new([moved]), Dirty(new DataWorldChunk(1, 0)));
        Assert.Single(Directory.GetFiles(Chunks));
        await repo.SaveAsync(Layout, new(default), Dirty(new DataWorldChunk(2, 0)));
        Assert.Empty((await new RepoWorld(_directory).LoadAsync(Layout)).Objects);
        Assert.Empty(Directory.GetFiles(Chunks));
        Assert.Empty(JsonNode.Parse(await File.ReadAllTextAsync(Manifest))!["chunks"]!.AsArray());
    }
    /// <summary>候选全部写入后、清单提交前中断，重载仍是完整旧状态；成功重试才变为完整新状态。</summary>
    [Fact]
    public async Task InterruptionBeforeManifestKeepsEntireOldStateAndRetryCommitsEntireNewState()
    {
        var repo = new RepoWorld(_directory);
        var old = new DataWorldSnapshot([Object(1), Object(70)]);
        await repo.SaveAsync(Layout, old, Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0)));
        var manifest = await File.ReadAllBytesAsync(Manifest);
        var next = new DataWorldSnapshot([old.Objects[0] with { Count = 9 }, Object(140)]);
        var interrupted = new RepoWorld(_directory) { BeforeManifestCommit = () => throw new IOException("模拟清单提交前中断") };
        await Assert.ThrowsAsync<IOException>(() => interrupted.SaveAsync(Layout, next, Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0), new DataWorldChunk(2, 0))).AsTask());
        Assert.Equal(manifest, await File.ReadAllBytesAsync(Manifest));
        Assert.Equal(4, Directory.GetFiles(Chunks).Length);
        AssertObjects(old.Objects, await repo.LoadAsync(Layout));
        await repo.SaveAsync(Layout, next, Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0), new DataWorldChunk(2, 0)));
        AssertObjects(next.Objects, await new RepoWorld(_directory).LoadAsync(Layout));
        Assert.Equal(2, Directory.GetFiles(Chunks).Length);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));
    }
    /// <summary>真实清单替换 IO 失败不改变提交点，解除占用后可以重试。</summary>
    [Fact]
    public async Task LockedManifestPreventsCommitWithoutTruncatingOldState()
    {
        var repo = new RepoWorld(_directory);
        var first = Object(1);
        await repo.SaveAsync(Layout, new([first]), Dirty(new DataWorldChunk(0, 0)));
        await using (var locked = new FileStream(Manifest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => repo.SaveAsync(Layout, new([first with { Count = 2 }]), Dirty(new DataWorldChunk(0, 0))).AsTask());
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "应拒绝替换被占用清单");
        }
        AssertObjects([first], await repo.LoadAsync(Layout));
        await repo.SaveAsync(Layout, new([first with { Count = 2 }]), Dirty(new DataWorldChunk(0, 0)));
        Assert.Equal(2, (await repo.LoadAsync(Layout)).Objects.Single().Count);
    }
    /// <summary>清理失败只报告诊断，已提交数据可读；后续成功保存重试清理且保留未知文件。</summary>
    [Fact]
    public async Task CleanupFailureDoesNotUndoCommitAndLaterSaveRetriesWithoutDeletingUnknownFiles()
    {
        var repo = new RepoWorld(_directory);
        var value = Object(1);
        await repo.SaveAsync(Layout, new([value]), Dirty(new DataWorldChunk(0, 0)));
        var old = Directory.GetFiles(Chunks).Single();
        var unknown = Path.Combine(Chunks, "external.json");
        await File.WriteAllTextAsync(unknown, "keep");
        var errors = new List<Exception>();
        var cleanupFails = new RepoWorld(_directory, error => { errors.Add(error); throw new Exception("诊断监听失败"); })
            { BeforeChunkDelete = _ => throw new IOException("模拟清理失败") };
        await cleanupFails.SaveAsync(Layout, new([value with { Count = 2 }]), Dirty(new DataWorldChunk(0, 0)));
        Assert.Single(errors);
        Assert.True(File.Exists(old));
        Assert.Equal(2, (await repo.LoadAsync(Layout)).Objects.Single().Count);
        await repo.SaveAsync(Layout, new([value with { Count = 3 }]), Dirty(new DataWorldChunk(0, 0)));
        Assert.False(File.Exists(old));
        Assert.Equal("keep", await File.ReadAllTextAsync(unknown));
        Assert.Equal(2, Directory.GetFiles(Chunks).Length);
    }
    /// <summary>同仓库各地图隔离，数据不套用旧对象的编号、范围、载荷及配置检查。</summary>
    [Fact]
    public async Task MapsAreIsolatedAndObjectsDoNotAcquireLegacyBusinessValidation()
    {
        var repo = new RepoWorld(_directory);
        var value = Object(-70) with { ObjectId = Guid.Empty, Revision = -5, Y = 999999, Count = -7, ItemId = -123, MonsterTypeId = "" };
        var chunk = Layout.ChunkAt(value);
        await repo.SaveAsync(Layout, new([value, value]), Dirty(chunk));
        AssertObjects([value, value], await repo.LoadAsync(Layout));
        Assert.Empty((await repo.LoadAsync(Layout with { MapId = 2 })).Objects);
        Assert.False(Directory.Exists(Path.Combine(_directory, "maps", "2")));
    }
    /// <summary>拒绝损坏或不完整的清单元数据，提交编号不可能成为任意引用路径。</summary>
    [Theory]
    [InlineData("syntax")]
    [InlineData("null")]
    [InlineData("missing")]
    [InlineData("version")]
    [InlineData("map")]
    [InlineData("layout")]
    [InlineData("layout-null")]
    [InlineData("layout-missing")]
    [InlineData("chunks-null")]
    [InlineData("reference-null")]
    [InlineData("duplicate-reference")]
    [InlineData("unsafe-commit")]
    [InlineData("empty-commit")]
    public async Task CorruptManifestFailsBeforePublishingData(string fault)
    {
        var repo = new RepoWorld(_directory);
        await repo.SaveAsync(Layout, new([Object(1)]), Dirty(new DataWorldChunk(0, 0)));
        var node = JsonNode.Parse(await File.ReadAllTextAsync(Manifest))!;
        switch (fault)
        {
            case "version": node["formatVersion"] = 999; break;
            case "map": node["mapId"] = 2; break;
            case "layout": node["layout"]!["width"] = 101; break;
            case "layout-null": node["layout"] = null; break;
            case "layout-missing": node["layout"]!.AsObject().Remove("tileSize"); break;
            case "chunks-null": node["chunks"] = null; break;
            case "reference-null": node["chunks"]![0] = null; break;
            case "duplicate-reference": node["chunks"]!.AsArray().Add(node["chunks"]![0]!.DeepClone()); break;
            case "unsafe-commit": node["chunks"]![0]!["commitId"] = "../../outside"; break;
            case "empty-commit": node["chunks"]![0]!["commitId"] = Guid.Empty.ToString(); break;
            case "missing": node.AsObject().Remove("chunks"); break;
        }
        await File.WriteAllTextAsync(Manifest, fault == "syntax" ? "{" : fault == "null" ? "null" : node.ToJsonString());
        var error = await Record.ExceptionAsync(() => repo.LoadAsync(Layout).AsTask());
        Assert.True(error is JsonException or InvalidDataException, error?.ToString() ?? "应拒绝清单");
    }
    /// <summary>引用块缺失、损坏、身份不匹配或被占用均失败，修复后完整读取而非部分结果。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("syntax")]
    [InlineData("null")]
    [InlineData("version")]
    [InlineData("map")]
    [InlineData("coordinate")]
    [InlineData("commit")]
    [InlineData("objects-null")]
    [InlineData("object-null")]
    [InlineData("locked")]
    public async Task ReferencedBlockFailureCanBeRepaired(string fault)
    {
        var repo = new RepoWorld(_directory);
        var values = new[] { Object(1), Object(70) };
        await repo.SaveAsync(Layout, new([.. values]), Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0)));
        var path = Directory.GetFiles(Chunks, "1_0.*").Single();
        var original = await File.ReadAllBytesAsync(path);
        var node = JsonNode.Parse(original)!;
        switch (fault)
        {
            case "version": node["formatVersion"] = 99; break;
            case "map": node["mapId"] = 2; break;
            case "coordinate": node["x"] = 5; break;
            case "commit": node["commitId"] = Guid.NewGuid().ToString(); break;
            case "objects-null": node["objects"] = null; break;
            case "object-null": node["objects"]![0] = null; break;
        }
        if (fault == "missing") File.Delete(path);
        else await File.WriteAllTextAsync(path, fault == "syntax" ? "{" : fault == "null" ? "null" : node.ToJsonString());
        using (var locked = fault == "locked" ? new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null)
            await Assert.ThrowsAnyAsync<Exception>(() => repo.LoadAsync(Layout).AsTask());
        await File.WriteAllBytesAsync(path, original);
        AssertObjects(values, await repo.LoadAsync(Layout));
    }
    /// <summary>构造带稳定身份的独立对象。</summary>
    internal static DataWorldObject Object(float x) => new() { ObjectId = Guid.NewGuid(), Revision = 1, X = x, Y = 1, ItemId = 7, Count = 1 };
    /// <summary>构造本次指定变化块集合。</summary>
    internal static ImmutableHashSet<DataWorldChunk> Dirty(params DataWorldChunk[] chunks) => chunks.ToImmutableHashSet();
    /// <summary>地图全量对象按内容比较，忽略不同块的恢复排列。</summary>
    internal static void AssertObjects(IEnumerable<DataWorldObject> expected, DataWorldSnapshot actual) =>
        Assert.Equal(expected.OrderBy(value => value.X).ThenBy(value => value.ObjectId), actual.Objects.OrderBy(value => value.X).ThenBy(value => value.ObjectId));
    /// <summary>仅清理本例生成的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
