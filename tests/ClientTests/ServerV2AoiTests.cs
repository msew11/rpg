using Rpg.Config;
using Rpg.ProtocolV2;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules.Role.Async;
using Rpg.ServerV2.Modules.Role.Cache;
using Rpg.ServerV2.Modules.World;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Modules.World.Handler;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>对应 server-v2-movement：地图边缘、对角跨块、无交集、空块与最新公开投影重试。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2AoiTests
{
    /// <summary>投影本身不能上线；首次完整，保留角色只发位置，离开明确移除。</summary>
    [Fact]
    public async Task AoiPublishesEmptyChunkDifferencesAndFinalRoleChanges()
    {
        using var env = ConfigTestFixture.CreateEnv(); using var config = new ConfigService(env);
        var context = new WorldContext(config, new DoubleLocator(), new(1, 1000, 1000, 32, 32), new([])); var handler = new WorldSyncHandler();
        var cache = context.Cache.Roles;
        var observer = Public(1, 0, 0); var other = Public(2, 100, 100);
        await handler.HandleAsync(context, new UpdateWorldRole { Role = observer, Revision = 1 }); Assert.Empty(cache.Value);
        cache.Membership(1, true); cache.Membership(2, true);
        await handler.HandleAsync(context, new UpdateWorldRole { Role = observer, Revision = 1 });
        await handler.HandleAsync(context, new UpdateWorldRole { Role = other, Revision = 1 });
        var initial = (await handler.HandleAsync(context, new GetWorldSnapshot { RoleId = 1, ServerTimeMs = 1000 })).Frame;
        Assert.Equal(1UL, initial.Sequence); Assert.Equal(4, initial.Snapshot.Map.Chunks.Count); Assert.Single(initial.Snapshot.Roles);
        other.Position.X = 200; other.Position.Target = new() { X = 300, Y = 100 };
        await handler.HandleAsync(context, new UpdateWorldRole { Role = other, Revision = 2 });
        var moved = (await handler.HandleAsync(context, Updates(1100))).Deliveries.Single().Notice.Frame;
        Assert.Single(moved.Delta.MovedRoles); Assert.Empty(moved.Delta.EnteredRoles); Assert.Null(moved.Delta.Map);
        observer.Position.X = 1030; observer.Position.Y = 1030;
        await handler.HandleAsync(context, new UpdateWorldRole { Role = observer, Revision = 2 });
        var diagonal = (await handler.HandleAsync(context, Updates(1200))).Deliveries.Single().Notice.Frame;
        Assert.Equal(5, diagonal.Delta.Map.EnteredChunks.Count); Assert.Empty(diagonal.Delta.Map.LeftChunks);
        Assert.All(diagonal.Delta.Map.EnteredChunks, chunk => Assert.Empty(chunk.Objects));
        observer.Position.X = 10000; observer.Position.Y = 10000;
        await handler.HandleAsync(context, new UpdateWorldRole { Role = observer, Revision = 3 });
        var distant = (await handler.HandleAsync(context, Updates(1300))).Deliveries.Single().Notice.Frame;
        Assert.Equal(9, distant.Delta.Map.EnteredChunks.Count); Assert.Equal(9, distant.Delta.Map.LeftChunks.Count); Assert.Contains(2L, distant.Delta.LeftRoleIds);
        cache.Membership(2, false); Assert.False(cache.Value.ContainsKey(2));
        var snapshot = (await handler.HandleAsync(context, new GetWorldSnapshot { RoleId = 1, ServerTimeMs = 1400 })).Frame;
        Assert.Equal(distant.Sequence + 1, snapshot.Sequence); Assert.Empty(snapshot.Snapshot.Roles);
    }
    /// <summary>Role提交不因World失败回滚；下一次只重试最新坐标和确认，不保存目标。</summary>
    [Fact]
    public async Task FailedProjectionRetriesLatestAuthoritativeState()
    {
        using var env = ConfigTestFixture.CreateEnv(); using var config = new ConfigService(env);
        await using var projection = new DoubleProjection();
        var locator = new DoubleLocator { Projection = projection.Ref };
        var data = DataCacheRole.CreateInitial(new() { RoleId = 1, Name = "重试", Level = 1, Gender = RoleGenderPb.Male, HeadId = 1 }, config.Map.Items[1]);
        var context = new RoleContext(config, locator, 1, new(data, [], [])) { Call = new(ServerTimeMs: 1000, MovementSpeed: 180) };
        var move = new MoveHandler(); var advance = new AdvanceRoleHandler();
        await move.HandleAsync(context, new Move { ActionId = 1, Target = new() { X = data.X + 500, Y = data.Y } });
        Assert.True(context.Cache.Movement.Value.ProjectionPending); Assert.Equal(1UL, context.Cache.Movement.Value.AcknowledgedAction);
        await advance.HandleAsync(context, new AdvanceRole { ServerTimeMs = 2000, MovementSpeed = 180 });
        Assert.Equal(data.X + 180, context.Cache.Role.Value.X); Assert.True(context.Cache.Movement.Value.ProjectionPending);
        projection.Fail = false;
        await advance.HandleAsync(context, new AdvanceRole { ServerTimeMs = 3000, MovementSpeed = 180 });
        Assert.False(context.Cache.Movement.Value.ProjectionPending); Assert.Equal(data.X + 360, projection.Latest!.Role.Position.X);
        Assert.Equal(1UL, projection.Latest.AcknowledgedAction);
        Assert.DoesNotContain("Target", System.Text.Json.JsonSerializer.Serialize(context.Cache.Snapshot()));
    }
    /// <summary>构造独立完整公开角色。</summary>
    private static RoleWorldStatePb Public(long id, float x, float y) => new() { RoleId = id, Profile = new() { Name = "玩家" + id, Gender = RoleGenderPb.Male, HeadId = 1, Level = 1 }, Status = new() { Health = 100, MaxHealth = 100 }, Equipment = new(), Position = new() { MapId = 1, X = x, Y = y, Speed = 180 } };
    /// <summary>本例只为观察者1构建差集。</summary>
    private static BuildWorldUpdates Updates(long time) => new() { ServerTimeMs = time, ObserverRoleIds = { 1 } };
    /// <summary>测试只开放单向Role到World入口，反向调用立即失败。</summary>
    private sealed class DoubleLocator : IActorLocator
    {
        /// <summary>可选投影目标。</summary>
        internal ActorRef? Projection;
        /// <summary>本例禁止账号查询。</summary>
        public ActorRef Login(string username) => throw new NotSupportedException();
        /// <summary>本例禁止World反向查询Role。</summary>
        public ActorRef Role(long roleId) => throw new NotSupportedException();
        /// <summary>返回受控World投影目标。</summary>
        public ActorRef World(int mapId) => Projection ?? throw new NotSupportedException();
    }
    /// <summary>仅模拟公开投影故障，不替换角色计算。</summary>
    private sealed class DoubleProjection : BaseActor
    {
        /// <summary>故障开关。</summary>
        internal bool Fail = true;
        /// <summary>最后一次交付的新协议对象。</summary>
        internal UpdateWorldRole? Latest;
        /// <summary>无需仓库。</summary>
        protected override ValueTask InitializeAsync() => ValueTask.CompletedTask;
        /// <summary>记录最新公开投影并模拟可恢复故障。</summary>
        protected override ValueTask<object?> ReceiveAsync(object message) { Latest = (UpdateWorldRole)message; if (Fail) throw new IOException("模拟World故障"); return ValueTask.FromResult<object?>(new UpdateWorldRoleRt()); }
    }
}
