using System.Text.Json.Nodes;
using Rpg.Server;
using Rpg.Server.Map;
using Rpg.Server.Entity;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-map-state 的地图隔离、提交中断、删除及失败重试。</summary>
public sealed class MapPersistenceTests : IDisposable
{
    /// <summary>每个用例独占的文件根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-map-" + Guid.NewGuid());
    /// <summary>测试地图，单块 320 逻辑像素。</summary>
    private static readonly MapLayout Rules = new(1, 100, 100, 32, 10);
    /// <summary>测试道具规则，不依赖客户端配置。</summary>
    private static readonly Dictionary<int, int> Items = new() { [1012] = 5 };

    /// <summary>创建测试地图，不隐式生成业务对象。</summary>
    private ServerMap Open(MapLayout? rules = null, RepoServerMap? repo = null) => new(rules ?? Rules, repo ?? new(_directory), Items);

    /// <summary>生成一个有稳定身份的怪物。</summary>
    private static MapObjectEntity Monster(float x = 100) => new() { X = x, Y = 100, MonsterTypeId = "test" };

    /// <summary>两地图同块互不覆盖；内存创建不提前写盘，落地后才能恢复。</summary>
    [Fact]
    public void MapsAreIsolatedAndOnlyCommittedChangesRecover()
    {
        var first = Open();
        var second = Open(Rules with { MapId = 2 });
        var a = first.Create(Monster()).After!;
        var b = second.Create(Monster()).After!;
        Assert.False(Directory.Exists(_directory));
        Assert.Empty(Open().Objects);
        first.Flush(); second.Flush();
        Assert.Equal(a, Assert.Single(Open().Objects).Value);
        Assert.Equal(b, Assert.Single(Open(Rules with { MapId = 2 }).Objects).Value);
        first.Update(a with { X = 900 });
        Assert.Equal(a, Assert.Single(Open().Objects).Value);
    }

    /// <summary>已写候选块但未提交清单时恢复旧位置，重试后只在新位置。</summary>
    [Fact]
    public void InterruptedCrossChunkCommitNeverLosesOrDuplicatesObject()
    {
        var map = Open();
        var original = map.Create(Monster()).After!;
        map.Flush();
        var fail = true;
        var repo = new RepoServerMap(_directory) { BeforeManifestCommit = () => { if (fail) throw new IOException("中断"); } };
        map = Open(repo: repo);
        var updated = map.Update(original with { X = 500 }).After!;
        Assert.Throws<IOException>(() => map.Flush());
        Assert.Equal(original, Assert.Single(Open().Objects).Value);
        Assert.Equal(updated, map.Objects[updated.ObjectId]);
        fail = false;
        map.Flush();
        var restored = Open();
        Assert.Empty(restored.InChunk((0, 0)));
        Assert.Equal(updated, Assert.Single(restored.InChunk((1, 0))));
        Assert.Single(restored.Objects);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "maps", "1", "chunks"), "*.json"));
    }

    /// <summary>清单替换被占用时保留旧提交，释放锁后同一内存能重试。</summary>
    [Fact]
    public void ManifestFailureRetainsLatestMemoryAndDirtyChunks()
    {
        var map = Open(); var original = map.Create(Monster()).After!; map.Flush();
        var changed = map.Update(original with { X = 500 }).After!;
        var path = new RepoServerMap(_directory).ManifestPath(1);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => map.Flush());
        Assert.Equal(original, Assert.Single(Open().Objects).Value);
        Assert.Equal(changed, map.Objects[original.ObjectId]);
        map.Flush();
        Assert.Equal(changed, Assert.Single(Open().Objects).Value);
    }

    /// <summary>多次修改同块只写最终版本，空检测不写，删除不因旧文件复活。</summary>
    [Fact]
    public void CoalescesWritesSkipsUnchangedAndPersistsEmptyChunk()
    {
        var writes = new List<string>();
        var map = Open(repo: new RepoServerMap(_directory, (path, _) => writes.Add(path)));
        var original = map.Create(Monster()).After!;
        var changed = map.Update(original with { X = 110 }).After!;
        map.Update(changed with { X = 120 });
        map.Flush();
        Assert.Equal(2, writes.Count);
        writes.Clear(); map.Flush(); Assert.Empty(writes);
        map.Remove(original.ObjectId); map.Flush();
        Assert.Single(writes);
        Assert.Empty(Open().Objects);
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "maps", "1", "chunks")));
        map.Create(Monster());
        var temporaryId = map.Objects.Keys.Single();
        map.Remove(temporaryId); writes.Clear(); map.Flush(); Assert.Empty(writes);
    }

    /// <summary>布局改变和缺失引用均报错，保留原文件且不重建空存档。</summary>
    [Fact]
    public void CorruptionAndLayoutMismatchAreNotEmptyMaps()
    {
        var map = Open(); map.Create(Monster()); map.Flush();
        var repo = new RepoServerMap(_directory);
        var path = repo.ManifestPath(1);
        var original = File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(() => Open(Rules with { ChunkSizeTiles = 5 }));
        Assert.Equal(original, File.ReadAllText(path));
        var manifest = JsonNode.Parse(original)!;
        var chunk = Path.Combine(Path.GetDirectoryName(path)!, "chunks", manifest["chunks"]![0]!["file"]!.GetValue<string>());
        File.Delete(chunk);
        Assert.Throws<InvalidDataException>(() => Open());
        Assert.Equal(original, File.ReadAllText(path));
        File.WriteAllText(path, "{}");
        Assert.Throws<InvalidDataException>(() => Open());
        Assert.Equal("{}", File.ReadAllText(path));
    }

    /// <summary>无清单的候选文件不参与恢复，非法修改不污染对象和待写集合。</summary>
    [Fact]
    public void OrphansAreIgnoredAndInvalidMutationsAreAtomic()
    {
        var repo = new RepoServerMap(_directory) { BeforeManifestCommit = () => throw new IOException("首次中断") };
        var map = Open(repo: repo); map.Create(Monster());
        Assert.Throws<IOException>(() => map.Flush());
        Assert.Empty(Open().Objects);
        map = Open();
        Assert.Throws<InvalidDataException>(() => map.Create(Monster(float.NaN)));
        Assert.Throws<InvalidDataException>(() => map.Create(new() { ItemId = 1012, Count = 6 }));
        var original = map.Create(Monster()).After!;
        Assert.Throws<InvalidDataException>(() => map.Create(original));
        Assert.Throws<InvalidDataException>(() => map.Update(original with { Revision = 3 }));
        Assert.Throws<InvalidDataException>(() => map.Update(original with { X = -1 }));
        Assert.Equal(original, Assert.Single(map.Objects).Value);
        Assert.Equal((1, 0), Rules.ChunkAt(320, 0));
        Assert.Equal(4, Rules.Neighborhood(0, 0).Count);
    }

    /// <summary>mock-server：角色缺少地图或头型时拒绝加载，不补默认值或修改文件。</summary>
    [Theory]
    [InlineData("mapId")]
    [InlineData("headId")]
    public void MissingRoleFieldsFailWithoutBackfill(string field)
    {
        var repo = new RepoServerRole(_directory);
        var role = repo.Create("缺少字段", ServerRoleGender.Male, 1);
        var path = repo.GetRolePath(role.Role.Value.RoleId);
        var data = JsonNode.Parse(File.ReadAllText(path))!;
        data.AsObject().Remove(field);
        var original = data.ToJsonString();
        File.WriteAllText(path, original);
        var error = Assert.Throws<InvalidDataException>(() => repo.Load(role.Role.Value.RoleId));
        Assert.Contains(path, error.Message);
        Assert.Equal(original, File.ReadAllText(path));
    }
    /// <summary>地图沿用 0 禁用及关闭补写；返回登录后对象仍在服务端内存。</summary>
    [Fact]
    public void DisabledPeriodicFlushStillCommitsOnTransportClose()
    {
        var server = new MockServer(_directory, new(8, 5, []), flushInterval: () => 0);
        var transport = new Rpg.Net.MockTransport(server);
        var value = server.CreateMapObject(1, Monster());
        server.Tick(100);
        Assert.False(Directory.Exists(_directory));
        server.ResetSession(2);
        Assert.Equal(value, server.GetMap(1).Objects[value.ObjectId]);
        transport.Dispose();
        var restored = new ServerMap(new(1, 1000, 1000, 32, 32), new(_directory), Items);
        Assert.Equal(value, Assert.Single(restored.Objects).Value);
    }

    /// <summary>清理失败不会撤销已提交地图，下次检测仅清理旧文件而不重复写盘。</summary>
    [Fact]
    public void CleanupFailureDoesNotUndoCommitAndCanRetry()
    {
        var writes = new List<string>();
        var repo = new RepoServerMap(_directory, (path, _) => writes.Add(path));
        var map = Open(repo: repo);
        var value = map.Create(Monster()).After!; map.Flush();
        var oldFile = Assert.Single(Directory.GetFiles(Path.Combine(_directory, "maps", "1", "chunks")));
        var updated = map.Update(value with { X = 500 }).After!;
        using (var locked = new FileStream(oldFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => map.Flush());
        Assert.Equal(updated, Assert.Single(Open().Objects).Value);
        writes.Clear(); map.Flush(); Assert.Empty(writes);
        Assert.False(File.Exists(oldFile));
    }

    /// <summary>移除仅属于测试目录的文件。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
