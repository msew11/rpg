using System.Collections.Immutable;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Common.Storage;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Role.Cache;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Storage;

namespace ClientTests;

/// <summary>对应 server-ground-items：真实文件中断恢复、延后清理与捕获基线。</summary>
public sealed class ServerItemBatchTests : IDisposable
{
    /// <summary>本例独占数据根。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-server-batch-" + Guid.NewGuid());
    /// <summary>每个提交领域中断后恢复两侧旧状态，修复重试提交新状态。</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("world")]
    [InlineData("login")]
    public async Task InterruptedBatchRestoresAllSidesAndCanRetry(string stage)
    {
        var roles = new RoleStorage(_root); var worlds = new WorldStorage(_root); var logins = new LoginStorage(_root);
        var layout = new DataWorldLayout(1, 100, 100, 32, 32);
        var original = new DataRoleSnapshot(new DataRole { RoleId = 1003, Name = "原角色", NextInstanceId = 1 }, [], []);
        var ground = new DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 1, X = 10, Y = 10, ItemId = 1012, Count = 1 };
        var chunks = ImmutableHashSet.Create(new DataWorldChunk(0, 0));
        var summaries = ImmutableArray.Create(new DataRoleSummary(1003, "原角色", 1, RoleGender.Male, 1, []));
        await roles.SaveAsync(1003, original, DataRoleTables.All);
        await worlds.SaveAsync(layout, new([ground]), chunks);
        await logins.SaveAsync("demo1", summaries);
        var priorChunk = Assert.Single(Directory.GetFiles(Path.Combine(_root, "maps/1/chunks")));
        var candidate = original with { Role = original.Role with { NextInstanceId = 2 },
            Items = [new(1, 1012, 0, 1)], Equipments = [new() { InstanceId = 1 }] };
        var roleSave = new DataRoleSave(1003, candidate, DataRoleTables.All);
        var mapSave = new DataWorldSave(layout, new([]), chunks);
        var loginSave = new DataLoginSave("demo1", [summaries[0] with { Name = "新摘要" }], true);
        var interrupted = new RepoItemBatch(_root, roles, worlds, logins) { AfterWrite = actual => { if (actual == stage) throw new IOException("模拟批次中断"); } };
        await Assert.ThrowsAsync<IOException>(() => interrupted.SaveAsync([roleSave], [mapSave], [loginSave]).AsTask());
        Assert.True(File.Exists(Path.Combine(_root, "items.pending.json"))); Assert.True(File.Exists(priorChunk));
        var recovered = new RepoItemBatch(_root, roles, worlds, logins);
        await recovered.RecoverAsync();
        Assert.Empty((await roles.LoadAsync(1003)).Items);
        Assert.Equal(ground, Assert.Single((await worlds.LoadAsync(layout)).Objects));
        Assert.Equal("原角色", Assert.Single(await logins.LoadAsync("demo1")).Name);
        await recovered.SaveAsync([roleSave], [mapSave], [loginSave]);
        Assert.Single((await roles.LoadAsync(1003)).Items); Assert.Empty((await worlds.LoadAsync(layout)).Objects);
        Assert.Equal("新摘要", Assert.Single(await logins.LoadAsync("demo1")).Name);
        Assert.False(File.Exists(priorChunk)); Assert.False(File.Exists(Path.Combine(_root, "items.pending.json")));
        Assert.False(File.Exists(Path.Combine(_root, "1003.pending.json")));
    }
    /// <summary>首次未保存文件中断后删除新增一侧，防止首次丢弃或跨角色转移制造副本。</summary>
    [Fact]
    public async Task RecoveryDeletesNewFilesAndRejectsUnsafeKeysBeforeAnyWrite()
    {
        var roles = new RoleStorage(_root); var worlds = new WorldStorage(_root); var logins = new LoginStorage(_root);
        var candidate = new DataRoleSnapshot(new DataRole { RoleId = 1003 }, [], []);
        var interrupted = new RepoItemBatch(_root, roles, worlds, logins) { AfterWrite = _ => throw new IOException("中断") };
        await Assert.ThrowsAsync<IOException>(() => interrupted.SaveAsync([new(1003, candidate, DataRoleTables.All)], [], []).AsTask());
        await new RepoItemBatch(_root, roles, worlds, logins).RecoverAsync();
        Assert.False(File.Exists(Path.Combine(_root, "1003.role.json")));
        await StorageJson.WriteAsync(Path.Combine(_root, "items.pending.json"), StorageJson.Encode(new Dictionary<string, byte[]?> { ["1003.role.json"] = [1], ["../outside.json"] = [2] }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new RepoItemBatch(_root, roles, worlds, logins).RecoverAsync().AsTask());
        Assert.False(File.Exists(Path.Combine(_root, "1003.role.json")));
    }
    /// <summary>保存只接受已捕获值，写盘期间后续移动仍是待写变化。</summary>
    [Fact]
    public void AcceptCapturedValueKeepsLaterMovementDirty()
    {
        var cache = new DataCacheRole(new DataRole { RoleId = 1003, X = 10 });
        cache.Commit(cache.Value with { X = 20 }); var captured = cache.Value;
        cache.Commit(cache.Value with { X = 30 }); cache.AcceptSaved(captured);
        Assert.True(cache.IsDirty); Assert.Equal(30, cache.Value.X);
        cache.AcceptSaved(); Assert.False(cache.IsDirty);
    }
    /// <summary>清理本例数据。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
