using Google.Protobuf;
using Rpg.Config;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Modules.Role.Cache;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Runtime;
using Codec = Rpg.ServerV2.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>对应 server-v2-movement：权威时间、控制接管、AOI增量、离线与重启。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2MovementTests : IDisposable
{
    /// <summary>本例临时数据根。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-movement-" + Guid.NewGuid());
    /// <summary>请求关联序号。</summary>
    private ulong _request;

    /// <summary>改目标先推进旧方向，旧动作无效，速度整体变化且到达不超调。</summary>
    [Fact]
    public void MovementMaterializesOldTargetAndSpeedAndStopsAtEndpoint()
    {
        var role = new DataCacheRole(new DataRole { X = 10, Y = 10 }); var movement = new DataCacheMovement();
        movement.Advance(role, 1000, 100); movement.Move(role, 1, 210, 10);
        movement.Advance(role, 1500, 200); Assert.Equal(60, role.Value.X); Assert.Equal(200, movement.Value.Speed);
        movement.Move(role, 2, 60, 210); movement.Move(role, 1, 999, 999);
        movement.Advance(role, 2000, 200); Assert.Equal(60, role.Value.X); Assert.Equal(110, role.Value.Y);
        movement.Advance(role, 9000, 200); Assert.Equal(210, role.Value.Y); Assert.Null(movement.Value.TargetX);
        movement.Stop(role, 10000, 300); Assert.Equal(0UL, movement.Value.AcknowledgedAction); Assert.Equal(300, movement.Value.Speed);
    }

    /// <summary>默认账号各自编号和列表隔离；无效凭据不能认证。</summary>
    [Fact]
    public async Task DefaultAccountsHaveSeparateRolesAndAcceptOnlyPassword123()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var ids = new HashSet<long>();
        foreach (var account in new[] { "admin", "demo1", "demo2" })
        {
            var connection = server.OpenConnection();
            Assert.IsType<LoginRt>(await Exchange(server, connection, new Login { Username = account, Password = "123" }));
            var role = Assert.Single(Assert.IsType<CreateRoleRt>(await Exchange(server, connection, new CreateRole { Name = account, Gender = RoleGenderPb.Male, HeadId = 1 })).Roles);
            Assert.True(ids.Add(role.RoleId));
            Assert.IsType<ErrorRt>(await Exchange(server, connection, new Login { Username = account, Password = "bad" }));
            Assert.Null(server.Session(connection).Username);
        }
    }

    /// <summary>多人完整入视野、位置增量、重同步、接管、旧连接重登及断线移除。</summary>
    [Fact]
    public async Task MultiplePlayersMoveResyncTakeOverAndDisappearOnDisconnect()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        await using var server = MockServerService.CreateInProcess(env, config);
        var one = server.OpenConnection(); var two = server.OpenConnection();
        var first = await Create(server, one, "demo1"); var second = await Create(server, two, "demo2");
        var entry = Assert.IsType<EnterWorldRt>(await Exchange(server, one, new EnterWorld { RoleId = first }));
        var initial = ReadFrame(server, one); Assert.Equal(1UL, initial.Sequence); Assert.NotNull(initial.Snapshot);
        await Exchange(server, two, new EnterWorld { RoleId = second });
        Assert.Equal(first, Assert.Single(ReadFrame(server, two).Snapshot.Roles).RoleId);
        await server.Server!.AdvanceAsync();
        Assert.Equal(second, Assert.Single(ReadFrame(server, one).Delta.EnteredRoles).RoleId);
        await Exchange(server, two, new Move { ActionId = 1, Target = new() { X = entry.State.Position.X + 500, Y = entry.State.Position.Y } });
        await Task.Delay(130); await server.Server.AdvanceAsync();
        var moved = ReadFrame(server, one); Assert.Empty(moved.Delta.EnteredRoles);
        var position = Assert.Single(moved.Delta.MovedRoles).Position;
        Assert.True(position.X > entry.State.Position.X); Assert.NotNull(position.Target); Assert.Equal(180, position.Speed);
        var resync = Assert.IsType<ResyncWorldRt>(await Exchange(server, one, new ResyncWorld())); Assert.NotNull(resync.Frame.Snapshot); Assert.True(resync.Frame.Sequence > moved.Sequence);
        var takeover = server.OpenConnection(); await Exchange(server, takeover, new Login { Username = "demo2", Password = "123" });
        await Exchange(server, takeover, new EnterWorld { RoleId = second });
        Assert.Null(server.Session(two).Username); Assert.Equal(0, server.Session(two).RoleId);
        Assert.Contains(ReadAll(server, two), message => message is SessionKickedNotice);
        Assert.IsType<ErrorRt>(await Exchange(server, two, new Move { ActionId = 99, Target = new() { X = 50, Y = 50 } }));
        Assert.IsType<LoginRt>(await Exchange(server, two, new Login { Username = "demo2", Password = "123" }));
        Assert.Null(ReadFrame(server, takeover).Self.Position.Target);
        await server.CloseConnectionAsync(takeover); await Task.Delay(120); await server.Server.AdvanceAsync();
        var left = ReadFrame(server, one); Assert.Contains(second, left.Delta.LeftRoleIds);
        await server.FlushAsync(); var saved = await new RepoRole(Path.Combine(_root, "v2")).LoadAsync(second);
        Assert.True(saved.Role.X > entry.State.Position.X);
    }

    /// <summary>运行时元数据拒绝邮箱内迟到的控制调用，原消息不含隔离字段。</summary>
    [Fact]
    public async Task RuntimeSidecarRejectsStaleQueuedWork()
    {
        var registry = new ActorCallRegistry(); var id = Guid.NewGuid(); registry.SetConnection(id, 1, 1); registry.SetControl(7, 1);
        await using var actor = new DoubleMailbox { Calls = registry };
        var first = actor.Ref.Ask<bool>(new object()); await actor.Entered.Task;
        var stale = actor.Ref.Ask<bool>(new object(), new DataActorCall(id, 1, 1, 7, 1));
        registry.SetControl(7, 2); actor.Release.SetResult(); await first;
        await Assert.ThrowsAsync<OperationCanceledException>(() => stale); Assert.Equal(1, actor.Executed);
        Assert.True(await actor.Ref.Ask<bool>(new object(), new DataActorCall(id, 1, 1, 7, 2)));
        registry.SetConnection(id, 5, 2);
        await Assert.ThrowsAsync<OperationCanceledException>(() => actor.Ref.Ask<bool>(new object(), new DataActorCall(id, 1, 1)));
        Assert.True(await actor.Ref.Ask<bool>(new object(), new DataActorCall(id, 5, 2)));
        Assert.DoesNotContain(Move.Descriptor.Fields.InDeclarationOrder(), field => field.Name.Contains("generation") || field.Name.Contains("session"));
    }
    /// <summary>关闭保存最后权威坐标，重新进入保持坐标且静止、不恢复动作确认。</summary>
    [Fact]
    public async Task RestartRestoresSavedCoordinatesWithoutRuntimeMovement()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        long role;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var connection = server.OpenConnection(); role = await Create(server, connection, "demo1");
            var entry = Assert.IsType<EnterWorldRt>(await Exchange(server, connection, new EnterWorld { RoleId = role })); ReadFrame(server, connection);
            Assert.IsType<ErrorRt>(await Exchange(server, connection, new Move { ActionId = 1 }));
            Assert.IsType<ErrorRt>(await Exchange(server, connection, new Move { ActionId = 1, Target = new() { X = float.NaN } }));
            await Exchange(server, connection, new Move { ActionId = 2, Target = new() { X = entry.State.Position.X + 500, Y = entry.State.Position.Y } });
            await Task.Delay(90); await server.Server!.AdvanceAsync();
        }
        var saved = await new RepoRole(Path.Combine(_root, "v2")).LoadAsync(role);
        Assert.True(saved.Role.X > 16016);
        await using var restarted = MockServerService.CreateInProcess(env, config);
        var id = restarted.OpenConnection(); await Exchange(restarted, id, new Login { Username = "demo1", Password = "123" });
        var restored = Assert.IsType<EnterWorldRt>(await Exchange(restarted, id, new EnterWorld { RoleId = role }));
        Assert.Equal(saved.Role.X, restored.State.Position.X); Assert.Null(restored.State.Position.Target);
        Assert.Equal(0UL, ReadFrame(restarted, id).AcknowledgedAction);
    }
    /// <summary>阻塞第一回合供控制版本在排队期间切换。</summary>
    private sealed class DoubleMailbox : BaseActor
    {
        /// <summary>已进入第一回合。</summary>
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>由测试释放第一回合。</summary>
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>实际执行次数。</summary>
        internal int Executed;
        /// <summary>无需存储初始化。</summary>
        protected override ValueTask InitializeAsync() => ValueTask.CompletedTask;
        /// <summary>只阻塞首个消息。</summary>
        protected override async ValueTask<object?> ReceiveAsync(object message) { if (++Executed == 1) { Entered.SetResult(); await Release.Task; } return true; }
    }
    /// <summary>通过真正账号处理器准备角色。</summary>
    private async Task<long> Create(MockServerService server, DataConnectionId connection, string username)
    {
        await Exchange(server, connection, new Login { Username = username, Password = "123" });
        return Assert.Single(Assert.IsType<CreateRoleRt>(await Exchange(server, connection, new CreateRole { Name = username, Gender = RoleGenderPb.Male, HeadId = 1 })).Roles).RoleId;
    }
    /// <summary>请求回复和推送共用通道，仅匹配本次关联编号。</summary>
    private async Task<IMessage> Exchange(MockServerService server, DataConnectionId id, IMessage request)
    {
        var number = ++_request; var generation = server.Session(id).Generation;
        await server.ReceiveAsync(id, Codec.Encode(request, generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        while (server.TryReadOutgoing(id, out var bytes)) { var envelope = Codec.ReadEnvelope(bytes); if (envelope.RequestId == number) return Codec.ReadPayload(envelope, false); }
        throw new InvalidOperationException("请求缺少回复");
    }
    /// <summary>提取一个世界推送供断言。</summary>
    private static WorldFramePb ReadFrame(MockServerService server, DataConnectionId id) => Assert.IsType<WorldUpdateNotice>(ReadAll(server, id).Last()).Frame;
    /// <summary>排空已有输出。</summary>
    private static List<IMessage> ReadAll(MockServerService server, DataConnectionId id)
    { var result = new List<IMessage>(); while (server.TryReadOutgoing(id, out var bytes)) result.Add(Codec.ReadPayload(Codec.ReadEnvelope(bytes), false)); return result; }
    /// <summary>删除本例临时文件。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
