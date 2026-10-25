using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>在 Godot 宿主验证 openspec/specs/map-aoi 的字节缓存、重同步及会话隔离。</summary>
[Trait("Runtime", "Godot")]
public sealed class MapCacheTests : RegisteredServiceTestScope
{
    /// <summary>真实服务器对象经字节队列到达唯一缓存，切角清空并重新获取最新对象。</summary>
    [Fact]
    public void ObjectsReachCacheAndReentryDoesNotDeleteServerState()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        var login = source.Login();
        var roleId = login.Roles[0].RoleId;
        var session = source.SelectRole(roleId);
        Assert.True(session.HasMap);
        Assert.Equal(9, session.Map!.Chunks.Count);
        var position = session.Role.Position;
        var value = source.Server.CreateMapObject(1, new() { X = position.X, Y = position.Y, MonsterTypeId = "test" });
        source.Net.Tick(0);
        Assert.Equal(value.ObjectId, Assert.Single(session.Map.Objects).Key);
        var prior = session.Map;
        session.LeaveRole();
        Assert.Null(session.Map);
        value = source.Server.UpdateMapObject(1, value with { X = position.X + 1 });
        source.SelectRole(roleId);
        Assert.NotSame(prior, session.Map);
        Assert.Equal(value.X, Assert.Single(session.Map!.Objects).Value.X);
        session.Logout(); Assert.Null(session.Map);
    }

    /// <summary>缺口暂停增量，Session 只发送一个在途重同步请求，返回快照恢复。</summary>
    [Fact]
    public void SequenceGapRequestsOneSnapshotAndRecovers()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        var session = source.SelectRole(source.Login().Roles[0].RoleId);
        var original = session.Map!;
        EventBus.Publish(new MapAoiNotice
        {
            RoleId = session.RoleId, MapId = 1, Sequence = original.Sequence + 2,
            Delta = new() { Center = new() { X = original.Center.X, Y = original.Center.Y } }
        });
        Assert.Same(original, session.Map);
        Assert.True(session.ResyncMapIfNeeded());
        Assert.False(session.ResyncMapIfNeeded());
        source.Net.Tick(0);
        Assert.True(session.Map!.Sequence > original.Sequence);
        Assert.False(session.ResyncMapIfNeeded());
        Assert.Empty(source.Errors);
    }

    /// <summary>非法批次中先出现的合法对象也不能污染原缓存，错误地图及旧序号忽略。</summary>
    [Fact]
    public void InvalidBatchDoesNotPartiallyApplyAndWrongIdentityIsIgnored()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        var session = source.SelectRole(source.Login().Roles[0].RoleId);
        var original = session.Map!;
        var position = session.Role.Position;
        var valid = new MapObjectPb { ObjectId = Guid.NewGuid().ToString("N"), Revision = 1, X = position.X, Y = position.Y,
            Monster = new() { MonsterTypeId = "test" } };
        var delta = new MapAoiDeltaPb { Center = new() { X = original.Center.X, Y = original.Center.Y } };
        delta.UpsertObjects.Add(valid);
        delta.UpsertObjects.Add(new MapObjectPb { ObjectId = "bad" });
        var notice = new MapAoiNotice { RoleId = session.RoleId, MapId = 1, Sequence = original.Sequence + 1, Delta = delta };
        EventBus.Publish(notice);
        Assert.Same(original, session.Map);
        Assert.Single(source.Errors);
        notice.MapId = 2; EventBus.Publish(notice);
        notice.MapId = 1; notice.Sequence = original.Sequence; EventBus.Publish(notice);
        Assert.Same(original, session.Map);
        Assert.Single(source.Errors);
    }

    /// <summary>PbData 复制协议，修改输入不能改变快照；块归属错误明确拒绝。</summary>
    [Fact]
    public void SnapshotCopiesMessagesAndValidatesChunkMembership()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        var session = source.SelectRole(source.Login().Roles[0].RoleId);
        MapAoiSnapshotPb? snapshot = null;
        using var subscription = EventBus.Subscribe<ResyncMapAoiRt>(response => snapshot = response.Snapshot);
        source.Net.Send(new ResyncMapAoi { RoleId = session.RoleId, MapId = 1 }); source.Net.Tick(0);
        Assert.NotNull(snapshot);
        var data = DataMapAoiSnapshotPbData.FromPb(snapshot);
        snapshot.Chunks.Clear();
        Assert.Equal(9, data.Chunks.Count);
        Assert.Throws<InvalidDataException>(() => DataMapAoiSnapshotPbData.FromPb(snapshot));
    }
}
