using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Hosting;
using Rpg.Server.Modules;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Login.Handler;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Async;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.World;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Storage;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-actor-protocol 与 server-runtime 的真实跨模块调用和边界。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerActorProtocolTests : ServerModuleTests
{
    /// <summary>使用独占的真实三表与地图存档，校验生产默认处理器按角色 MapId 查询且完全只读。</summary>
    [Fact]
    public async Task DefaultRoleAsyncHandlerQueriesLoadedWorldWithoutWritingStorage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-server-protocol-" + Guid.NewGuid());
        try
        {
            var roles = new RoleStorage(directory);
            var worlds = new WorldStorage(directory);
            var map = Config.Map.Items[1];
            var layout = new DataWorldLayout(1, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
            await roles.SaveAsync(10, new(new() { RoleId = 10, MapId = 1 }, [], []), DataRoleTables.All);
            await roles.SaveAsync(20, new(new() { RoleId = 20, MapId = 99 }, [], []), DataRoleTables.All);
            await worlds.SaveAsync(layout, new([new() { ObjectId = Guid.NewGuid(), X = 1 }, new() { ObjectId = Guid.NewGuid(), X = 2 }]),
                [new DataWorldChunk(0, 0)]);
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
            foreach (var path in files.Keys) File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1));
            await using (var modules = new ServerModules(Config, new DoubleServerStorage(), roles, worlds))
            {
                Assert.Equal(2, (await modules.Role(10).Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount())).Count);
                Assert.Equal(2, (await modules.World(1).Ask<GetWorldObjectCountRt>(new GetWorldObjectCount())).Count);
                await Assert.ThrowsAsync<InvalidDataException>(() => modules.Role(20).Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount()));
                Assert.Equal(ActorState.RUN, modules.Role(20).State);
                await modules.FlushAsync();
            }
            Assert.Equal(files.Keys.Order(), Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order());
            foreach (var (path, bytes) in files)
            {
                Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
                Assert.Equal(2000, File.GetLastWriteTimeUtc(path).Year);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>目标 Handler 跨 await 时源角色保持串行，独立角色继续；处理错误原样传播且两端邮箱可继续。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossActorAwaitKeepsSourceTurnAndPropagatesTargetFailure(bool fail)
    {
        var handler = new DoubleBlockedWorldHandler(fail);
        await using var modules = Modules(role: _ => new ActorHandlers<RoleContext>()
            .Register<GetRoleWorldObjectCount, GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCountHandler())
            .Register<DataReadRoleId, long>(new DoubleReadRoleIdHandler()), world: _ => new ActorHandlers<WorldContext>()
            .Register<GetWorldObjectCount, GetWorldObjectCountRt>(handler));
        var actor = modules.Role(10);
        var query = actor.Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount());
        Task<long> queued;
        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = actor.Ask(new DataReadRoleId());
            Assert.False(query.IsCompleted);
            Assert.False(queued.IsCompleted);
            Assert.Equal(20, await modules.Role(20).Ask(new DataReadRoleId()).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(queued.IsCompleted);
        }
        finally { handler.Release.TrySetResult(); }
        if (fail) Assert.Same(handler.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => query));
        else Assert.Equal(7, (await query.WaitAsync(TimeSpan.FromSeconds(5))).Count);
        Assert.Equal(10, await queued.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(7, (await actor.Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount())).Count);
    }

    /// <summary>生成消息实例直接进入目标 Handler，无序列化复制；错配回复类型不会终止邮箱。</summary>
    [Fact]
    public async Task InternalProtoPassesDirectlyAndWrongReplyTypeDoesNotPoisonMailbox()
    {
        var handler = new DoubleDirectWorldHandler();
        await using var modules = Modules(world: _ => new ActorHandlers<WorldContext>()
            .Register<GetWorldObjectCount, GetWorldObjectCountRt>(handler));
        var request = new GetWorldObjectCount();
        var actor = modules.World(1);
        var reply = await actor.Ask<GetWorldObjectCountRt>(request);
        Assert.Same(request, handler.Seen);
        Assert.Same(reply, handler.Reply);
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.Ask<GetRoleWorldObjectCountRt>(new GetWorldObjectCount()));
        Assert.Equal(7, (await actor.Ask<GetWorldObjectCountRt>(new GetWorldObjectCount())).Count);
    }

    /// <summary>全部 Context 的定位能力来自所属服务，定位不初始化；Context 不拥有关闭权限。</summary>
    [Fact]
    public async Task ContextLocatorsStayWithinOwningServiceAndOnlyOwnerStopsActors()
    {
        using var env = ConfigTestFixture.CreateEnv();
        var firstModules = LocatorModules();
        var secondModules = LocatorModules();
        await using var first = new MockServerService(env, firstModules);
        await using var second = new MockServerService(env, secondModules);
        foreach (var modules in new[] { firstModules, secondModules })
        {
            foreach (var actor in new[] { modules.Login("alice"), modules.Role(10), modules.World(1) })
            {
                var found = await actor.Ask(new DataLocateTargets());
                Assert.Same(modules.Login("target"), found.Login);
                Assert.Same(modules.Role(99), found.Role);
                Assert.Same(modules.World(99), found.World);
                Assert.All(new[] { found.Login, found.Role, found.World }, reference => Assert.Equal(ActorState.INIT, reference.State));
            }
        }
        Assert.NotSame(firstModules.World(99), secondModules.World(99));
        var context = new RoleContext(Config, firstModules, 10, new(new() { MapId = 1 }, [], []));
        Assert.Same(firstModules, context.Actors);
        Assert.False((object)context is IDisposable or IAsyncDisposable);
        var stopped = context.Actors.World(99);
        await first.DisposeAsync();
        Assert.Equal(ActorState.STOP, stopped.State);
        Assert.Throws<ObjectDisposedException>(() => context.Actors.World(99));
        Assert.Equal(ActorState.INIT, secondModules.World(99).State);
        Assert.False(Config.IsDisposed);
        Assert.Equal("actors", Assert.Throws<ArgumentNullException>(() => new LoginContext(Config, null!, "alice", new([]))).ParamName);
        Assert.Equal("actors", Assert.Throws<ArgumentNullException>(() => new RoleContext(Config, null!, 10, new(new(), [], []))).ParamName);
        Assert.Equal("actors", Assert.Throws<ArgumentNullException>(() => new WorldContext(Config, null!, new(1, 100, 100, 32, 2), new([]))).ParamName);
    }

    /// <summary>调用者提供注册工厂时不隐式混入生产示例，维持冻结注册表的调用者所有权。</summary>
    [Fact]
    public async Task ExplicitHandlerFactoriesKeepCallerControl()
    {
        await using var modules = Modules(role: _ => new(), world: _ => new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => modules.Role(10).Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => modules.World(1).Ask<GetWorldObjectCountRt>(new GetWorldObjectCount()));
    }

    /// <summary>生成描述符具有独立身份，客户端编码与伪造信封均拒绝全部内部消息，默认登录仍未实现。</summary>
    [Fact]
    public async Task GeneratedInternalMessagesStayOutsideClientCodecAndLoginBehavior()
    {
        using var env = ConfigTestFixture.CreateEnv();
        await using var server = new MockServerService(env, Modules());
        var connection = server.OpenConnection();
        IMessage[] messages = [new GetWorldObjectCount(), new GetWorldObjectCountRt(), new GetRoleWorldObjectCount(), new GetRoleWorldObjectCountRt(), new PrepareRole(), new PrepareRoleRt(), new GetWorldEntrySnapshot(), new GetWorldEntrySnapshotRt()];
        foreach (var message in messages)
        {
            Assert.Equal("Rpg.Server.ActorProtocol", message.GetType().Namespace);
            Assert.Equal("rpg.actor." + message.GetType().Name, message.Descriptor.FullName);
            Assert.All(message.Descriptor.File.Dependencies, dependency => Assert.Equal("rpg", dependency.Package));
            Assert.Equal(ErrorCodePb.UnknownMessage, Assert.Throws<ServerRequestException>(() => MessageCodec.Encode(message, 1, 42, true)).Code);
            Assert.Equal(ErrorCodePb.UnknownMessage, Assert.Throws<ServerRequestException>(() => MessageCodec.Encode(message, 1, 42, false)).Code);
            var forged = new EnvelopePb { Generation = 1, RequestId = 42, MessageType = message.Descriptor.FullName, Payload = message.ToByteString() };
            await server.ReceiveAsync(connection, forged.ToByteArray());
            Assert.True(server.TryReadOutgoing(connection, out var bytes));
            var envelope = MessageCodec.ReadEnvelope(bytes);
            Assert.Equal(42UL, envelope.RequestId);
            Assert.Equal(1UL, envelope.Generation);
            Assert.Equal(ErrorCodePb.UnknownMessage, Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(envelope, false)).Code);
        }
        Assert.Equal("rpg.Login", Login.Descriptor.FullName);
        await server.ReceiveAsync(connection, MessageCodec.Encode(new Login { Username = "alice" }, 1, 43, true));
        Assert.True(server.TryReadOutgoing(connection, out var packet));
        Assert.Equal(ErrorCodePb.InvalidCredentials, Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false)).Code);
        Assert.Null(server.Session(connection).Username);
    }

    /// <summary>建立带确定角色 MapId 的模块，空可选工厂使用生产示例。</summary>
    private DoubleServerModules Modules(Func<long, ActorHandlers<RoleContext>>? role = null, Func<int, ActorHandlers<WorldContext>>? world = null) =>
        new(Config, new DoubleServerStorage(), new DoubleRoleStorage(), new DoubleServerStorage(), LoginHandlers, role, world);
    /// <summary>使用正式未实现登录处理器，避免测试成功替身掩盖默认行为。</summary>
    private static ActorHandlers<LoginContext> LoginHandlers(string _) => new ActorHandlers<LoginContext>().Register<Login, LoginRt>(new LoginHandler());
    /// <summary>三个 Context 共用只返回引用的测试契约，目标身份从未被投递消息。</summary>
    private DoubleServerModules LocatorModules() => new(Config, new DoubleServerStorage(), new DoubleRoleStorage(), new DoubleServerStorage(),
        _ => new ActorHandlers<LoginContext>().Register<DataLocateTargets, DataTargets>(new DoubleLocateHandler<LoginContext>()),
        _ => new ActorHandlers<RoleContext>().Register<DataLocateTargets, DataTargets>(new DoubleLocateHandler<RoleContext>()),
        _ => new ActorHandlers<WorldContext>().Register<DataLocateTargets, DataTargets>(new DoubleLocateHandler<WorldContext>()));

    /// <summary>服务角色所属地图查询契约，返回完整且确定的不可变角色基线。</summary>
    private sealed class DoubleRoleStorage : IRoleStorage
    {
        /// <summary>所有角色都明确归属地图 1，不使用默认 MapId 掩盖定位行为。</summary>
        public ValueTask<DataRoleSnapshot> LoadAsync(long roleId) => ValueTask.FromResult(new DataRoleSnapshot(new() { RoleId = roleId, MapId = 1 }, [], []));
        /// <summary>只读验收不得触发保存。</summary>
        public ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables) => throw new InvalidOperationException("只读查询不应保存角色");
    }
    /// <summary>仅请求源角色身份，不携带上下文。</summary>
    private sealed record DataReadRoleId : IActorRequest<long>;
    /// <summary>服务源邮箱串行契约的本地只读处理器。</summary>
    private sealed class DoubleReadRoleIdHandler : IActorHandler<RoleContext, DataReadRoleId, long>
    {
        /// <summary>返回源分片身份以证明本回合已经执行。</summary>
        public ValueTask<long> HandleAsync(RoleContext context, DataReadRoleId request) => ValueTask.FromResult(context.Cache.RoleId);
    }
    /// <summary>为跨 await、目标异常与回合恢复契约提供确定性屏障。</summary>
    private sealed class DoubleBlockedWorldHandler(bool fail) : IActorHandler<WorldContext, GetWorldObjectCount, GetWorldObjectCountRt>
    {
        /// <summary>目标首回合已进入。</summary>
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>由测试最终放行，失败断言也不得遗留阻塞邮箱。</summary>
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>用于验证异常未经包装原样传播。</summary>
        internal InvalidOperationException Failure { get; } = new("目标处理失败");
        /// <summary>仅首回合注入故障，下一回合验证邮箱恢复。</summary>
        private bool _first = true;
        /// <summary>等待目标屏障后返回或抛出受控错误。</summary>
        public async ValueTask<GetWorldObjectCountRt> HandleAsync(WorldContext context, GetWorldObjectCount request)
        {
            var first = _first;
            _first = false;
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            if (first && fail) throw Failure;
            return new() { Count = 7 };
        }
    }
    /// <summary>仅为对象直达契约留存引用用于比较，测试不修改已移交对象。</summary>
    private sealed class DoubleDirectWorldHandler : IActorHandler<WorldContext, GetWorldObjectCount, GetWorldObjectCountRt>
    {
        /// <summary>收到的原请求，用于确认没有序列化复制。</summary>
        internal GetWorldObjectCount? Seen { get; private set; }
        /// <summary>本回合创建的新回复，仅用于确认返回同一实例。</summary>
        internal GetWorldObjectCountRt? Reply { get; private set; }
        /// <summary>记录原请求并直接返回新回复；记录后不再修改协议对象。</summary>
        public ValueTask<GetWorldObjectCountRt> HandleAsync(WorldContext context, GetWorldObjectCount request)
        { Seen = request; Reply = new() { Count = 7 }; return ValueTask.FromResult(Reply); }
    }
    /// <summary>只请求定位，不在消息中传入工作委托。</summary>
    private sealed record DataLocateTargets : IActorRequest<DataTargets>;
    /// <summary>只包含消息引用的不可变验收结果。</summary>
    /// <param name="Login">账号目标引用。</param>
    /// <param name="Role">角色目标引用。</param>
    /// <param name="World">地图目标引用。</param>
    private sealed record DataTargets(ActorRef Login, ActorRef Role, ActorRef World);
    /// <summary>验证全部 Context 借用同一所属定位接口的契约。</summary>
    private sealed class DoubleLocateHandler<TContext> : IActorHandler<TContext, DataLocateTargets, DataTargets> where TContext : ModuleContext
    {
        /// <summary>仅获取尚未初始化的目标分片，不返回上下文或存储。</summary>
        public ValueTask<DataTargets> HandleAsync(TContext context, DataLocateTargets request) =>
            ValueTask.FromResult(new DataTargets(context.Actors.Login("target"), context.Actors.Role(99), context.Actors.World(99)));
    }
}
