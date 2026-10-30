using System.Collections.Immutable;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Cache;
using Rpg.ServerV2.Modules.Role.Cache;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.World.Cache;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Protocol;

namespace ClientTests;

/// <summary>验证 server-v2-storage 的子缓存修改、聚合基线与不可变导出隔离契约。</summary>
public sealed class ServerV2DataCacheTests
{
    /// <summary>直接修改各表必须立即影响聚合状态，成功保存后各表共同建立新基线。</summary>
    [Fact]
    public void ChildCommitsUpdateAggregateAndSavedBaselineWithoutChangingExports()
    {
        var cache = new RoleCache(10, new(new DataRole { RoleId = 10 }, default, default));
        var original = cache.Snapshot();
        Assert.Empty(original.Items);
        Assert.Empty(original.Equipments);
        Assert.Equal(DataRoleTables.None, cache.DirtyTables);
        cache.Role.Commit(original.Role with { Name = "修改" });
        cache.Items.Commit([new(1, 3, 0, 1)]);
        cache.Equipments.Commit([new() { InstanceId = 1 }]);
        Assert.Equal(DataRoleTables.All, cache.DirtyTables);
        Assert.Equal("", original.Role.Name);
        Assert.Empty(original.Items);
        Assert.Empty(original.Equipments);
        var changed = cache.Snapshot();
        Assert.Throws<ArgumentNullException>(() => cache.Commit(original with { Role = null! }));
        Assert.Equal(changed, cache.Snapshot());
        cache.AcceptSaved();
        Assert.Equal(DataRoleTables.None, cache.DirtyTables);
        cache.Commit(original);
        Assert.Equal(DataRoleTables.All, cache.DirtyTables);
        cache.Commit(changed);
        Assert.Equal(DataRoleTables.None, cache.DirtyTables);
        cache.Items.Commit(default);
        Assert.Equal(DataRoleTables.Item, cache.DirtyTables);
        Assert.Empty(cache.Items.Value);
    }

    /// <summary>摘要管理归数据缓存；失败不改值，墓碑被保存且编号不复用。</summary>
    [Fact]
    public void SummaryCacheOwnsValidationTombstonesAndVisibleOrdering()
    {
        var cache = new LoginCache("demo1", new(default));
        cache.Summaries.Create("  Alpha  ", RoleGender.Male, 1);
        var created = cache.Snapshot();
        Assert.True(cache.SummariesDirty);
        Assert.Equal(1003, Assert.Single(cache.Summaries.Visible()).RoleId);
        var duplicate = Assert.Throws<ServerRequestException>(() => cache.Summaries.Create("alpha", RoleGender.Female, 74));
        Assert.Equal(ErrorCodePb.RoleNameConflict, duplicate.Code);
        Assert.Equal(created, cache.Snapshot());
        cache.AcceptSaved();
        cache.Summaries.Delete(1003);
        Assert.True(cache.SummariesDirty);
        Assert.Empty(cache.Summaries.Visible());
        Assert.True(Assert.Single(cache.Summaries.Value).IsDeleted);
        Assert.False(Assert.Single(created.Summaries).IsDeleted);
        cache.AcceptSaved();
        cache.Summaries.Create("Alpha", RoleGender.Female, 74);
        Assert.Equal(1004, Assert.Single(cache.Summaries.Visible()).RoleId);
        cache.Summaries.Commit([cache.Summaries.Value[1], cache.Summaries.Value[0]]);
        Assert.Equal(1004, Assert.Single(cache.Summaries.Visible()).RoleId);
    }

    /// <summary>地图子缓存直接提交时保留块语义，跨块移动及恢复与聚合待写状态一致。</summary>
    [Fact]
    public void WorldChildTracksMoveEndsAndIgnoresOrderingBetweenChunks()
    {
        var first = new DataWorldObject { ObjectId = Guid.NewGuid(), X = 0, Y = 0 };
        var second = new DataWorldObject { ObjectId = Guid.NewGuid(), X = 100, Y = 0 };
        var cache = new WorldCache(new(1, 20, 20, 10, 10), new([first, second]));
        var original = cache.Snapshot();
        cache.Objects.Commit([second, first]);
        Assert.False(cache.Objects.IsDirty);
        Assert.Empty(cache.DirtyChunks);
        cache.Objects.Commit([second, first with { X = 200 }]);
        Assert.True(cache.Objects.IsDirty);
        Assert.True(ImmutableHashSet.Create(new DataWorldChunk(0, 0), new DataWorldChunk(2, 0)).SetEquals(cache.DirtyChunks));
        Assert.Equal(0, original.Objects[0].X);
        cache.Objects.Commit(original.Objects);
        Assert.Empty(cache.DirtyChunks);
        cache.Objects.Commit(default);
        Assert.Equal(2, cache.DirtyChunks.Count);
        cache.AcceptSaved();
        Assert.False(cache.Objects.IsDirty);
        Assert.Empty(cache.Snapshot().Objects);
        Assert.Empty(cache.DirtyChunks);
    }
}
