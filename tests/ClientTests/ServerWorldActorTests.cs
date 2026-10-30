using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Rpg.Config;
using Rpg.Server.Hosting;
using Rpg.Server.Modules;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Protocol;
using Rpg.Server.Modules.World;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Storage;
using Rpg.Server.Runtime;
using static ClientTests.ServerWorldStorageTests;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-storage 的地图 INIT、内容基线、邮箱隔离与重试。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerWorldActorTests : IDisposable
{
    /// <summary>配置和存档均为用例独占目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-world-actor-" + Guid.NewGuid());
    /// <summary>借用配置前建立的环境。</summary>
    private readonly EnvService _env;
    /// <summary>一次加载两张地图，不在 Actor INIT 重新读取 JSON。</summary>
    private readonly ConfigService _config;
    /// <summary>建立两张不同规格地图，保持正式配置目录不变。</summary>
    public ServerWorldActorTests()
    {
        var configDirectory = Path.Combine(_directory, "config");
        Directory.CreateDirectory(configDirectory);
        foreach (var name in new[] { "map", "item", "basic", "equipment" })
            File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", name + ".json"), Path.Combine(configDirectory, name + ".json"));
        var path = Path.Combine(configDirectory, "map.json");
        var maps = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var second = maps[0]!.DeepClone();
        second["id"] = 2; second["width"] = 200; second["height"] = 300; second["overrides"] = new JsonArray();
        maps.Add(second);
        File.WriteAllText(path, maps.ToJsonString());
        _env = new(Path.Combine(_directory, "settings"), Path.Combine(_directory, "server"), configDirectory: configDirectory,
            schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));
        _config = new(_env);
    }
    /// <summary>本例实际 存档根。</summary>
    private string Storage => Path.GetFullPath(_env.ServerDirectory);
    /// <summary>复制已加载的指定地图规格。</summary>
    private DataWorldLayout Map(int id)
    {
        var map = _config.Map.Items[id];
        return new(id, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
    }
    /// <summary>首消息等待完整加载，同分片只加载一次；独立地图工作且 Flush 不启动闲置分片。</summary>
    [Fact]
    public async Task FirstHandlerWaitsForCompleteLoadAndOtherMapCanProgress()
    {
        var release = Signal();
        var repo = new DoubleWorldRepo(new RepoWorld(Storage)) { LoadRelease = release.Task };
        var value = Object(1);
        await new RepoWorld(Storage).SaveAsync(Map(1), new([value]), Dirty(new DataWorldChunk(0, 0)));
        File.Delete(Path.Combine(_directory, "config", "map.json"));
        await using var modules = Modules(repo);
        var actor = modules.World(1);
        Assert.Same(actor, modules.World(1));
        var unused = modules.World(3);
        Assert.Equal(0, repo.LoadCount);
        var first = actor.Ask(new DataReadWorld());
        await repo.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = actor.Ask(new DataReadWorld());
        Assert.False(first.IsCompleted);
        Assert.Empty((await modules.World(2).Ask(new DataReadWorld()).WaitAsync(TimeSpan.FromSeconds(5))).Objects);
        release.SetResult();
        AssertObjects([value], await first);
        AssertObjects([value], await second);
        await modules.FlushAsync();
        Assert.Equal(2, repo.LoadCount);
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ActorState.INIT, unused.State);
        Assert.Equal(200, repo.Layouts[2].Width);
        Assert.Equal(1000, repo.Layouts[1].Width);
    }
    /// <summary>未知地图在仓库调用前失败；失败引用可替换，必需仓库同步拒绝 null。</summary>
    [Fact]
    public async Task UnknownMapDoesNotPerformStorageIo()
    {
        var repo = new DoubleWorldRepo(new RepoWorld(Storage));
        await using var modules = Modules(repo);
        var actor = modules.World(99);
        await Assert.ThrowsAsync<InvalidDataException>(() => actor.Ask(new DataReadWorld()));
        Assert.Equal(0, repo.LoadCount);
        Assert.False(Directory.Exists(Storage));
        Assert.NotSame(actor, modules.World(99));
        Assert.Throws<ArgumentNullException>(() => new WorldActor(_config, modules, 1, null!, Handlers()));
    }
    /// <summary>加载失败拒绝同 INIT 的所有等待者，修复后目录重建分片再加载。</summary>
    [Fact]
    public async Task InitializationFailureRejectsAllWaitersAndNewReferenceRetries()
    {
        var release = Signal();
        var repo = new DoubleWorldRepo(new RepoWorld(Storage)) { LoadRelease = release.Task, FailLoad = true };
        await using var modules = Modules(repo);
        var first = modules.World(1);
        var one = first.Ask(new DataReadWorld());
        await repo.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var two = first.Ask(new DataReadWorld());
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => one);
        await Assert.ThrowsAsync<IOException>(() => two);
        repo.FailLoad = false;
        var retry = modules.World(1);
        Assert.NotSame(first, retry);
        Assert.Empty((await retry.Ask(new DataReadWorld())).Objects);
        Assert.Equal(2, repo.LoadCount);
    }
    /// <summary>真实文件清单或引用块损坏以及占用均不能发布部分 Context，修复后新分片完整恢复。</summary>
    [Theory]
    [InlineData("manifest")]
    [InlineData("null")]
    [InlineData("block-missing")]
    [InlineData("block-json")]
    [InlineData("io")]
    public async Task RepairingStoredFilesAllowsFreshActorInitialization(string fault)
    {
        var repo = new RepoWorld(Storage);
        var layout = Map(1);
        var values = new[] { Object(1), Object(1100) };
        await repo.SaveAsync(layout, new([.. values]), Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0)));
        var mapDirectory = Path.Combine(Storage, "maps", "1");
        var path = fault.StartsWith("block") ? Directory.GetFiles(Path.Combine(mapDirectory, "chunks"), "1_0.*").Single() : Path.Combine(mapDirectory, "manifest.json");
        var original = await File.ReadAllBytesAsync(path);
        if (fault == "block-missing") File.Delete(path);
        else if (fault != "io") await File.WriteAllTextAsync(path, fault == "null" ? "null" : "{");
        await using var modules = Modules(repo);
        var failed = modules.World(1);
        using (var locked = fault == "io" ? new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null)
            await Assert.ThrowsAnyAsync<Exception>(() => failed.Ask(new DataReadWorld()));
        await File.WriteAllBytesAsync(path, original);
        var restored = modules.World(1);
        Assert.NotSame(failed, restored);
        AssertObjects(values, await restored.Ask(new DataReadWorld()));
    }
    /// <summary>内容相同的新数组不脏；移动标记两端，连续修改合并，恢复原值及默认数组不写盘。</summary>
    [Fact]
    public async Task ContextComparesContentsTracksBothMoveEndsAndCancelsRestoredChanges()
    {
        var value = Object(1);
        var layout = Map(1);
        await using var locator = Modules(new DoubleServerRepo());
        var context = new WorldContext(_config, locator, layout, new([value]));
        context.Cache.Commit(new([value with { }]));
        Assert.Empty(context.Cache.DirtyChunks);
        context.Cache.Commit(new([value with { X = 1100 }]));
        Assert.True(Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(1, 0)).SetEquals(context.Cache.DirtyChunks));
        context.Cache.Commit(new([value with { X = 2100 }]));
        Assert.True(Dirty(new DataWorldChunk(0, 0), new DataWorldChunk(2, 0)).SetEquals(context.Cache.DirtyChunks));
        context.Cache.Commit(new([value with { }]));
        Assert.Empty(context.Cache.DirtyChunks);
        var empty = new WorldContext(_config, locator, layout, new(default));
        empty.Cache.Commit(new(default));
        Assert.Empty(empty.Cache.Objects.Value);
        Assert.Empty(empty.Cache.DirtyChunks);
        Assert.Throws<ArgumentNullException>(() => context.Cache.Commit(null!));
        var repo = new DoubleWorldRepo(new RepoWorld(Storage));
        await using var modules = Modules(repo);
        var actor = modules.World(1);
        await actor.Ask(new DataWriteWorld(new([value])));
        await actor.Ask(new DataWriteWorld(new(default)));
        await modules.FlushAsync();
        Assert.Equal(0, repo.SaveCount);
        Assert.False(Directory.Exists(Storage));
    }
    /// <summary>本地图保存期间不进入下一回合，其他地图继续；失败内存及脏块保留并可重试。</summary>
    [Fact]
    public async Task SaveOwnsMailboxAndFailureRetainsSnapshotAndDirtyChunksForRetry()
    {
        var release = Signal();
        var repo = new DoubleWorldRepo(new RepoWorld(Storage)) { SaveRelease = release.Task, FailSave = true };
        await using var modules = Modules(repo);
        var actor = modules.World(1);
        var value = Object(1);
        await actor.Ask(new DataWriteWorld(new([value])));
        var final = new DataWorldSnapshot([value with { Count = 9 }]);
        await actor.Ask(new DataWriteWorld(final));
        var flushing = actor.Ask(new DataFlush());
        await repo.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var read = actor.Ask(new DataReadWorld());
        Assert.False(read.IsCompleted);
        Assert.Empty((await modules.World(2).Ask(new DataReadWorld()).WaitAsync(TimeSpan.FromSeconds(5))).Objects);
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => flushing);
        AssertObjects(final.Objects, await read);
        Assert.Single(await actor.Ask(new DataWriteWorld(final)));
        repo.FailSave = false;
        await modules.FlushAsync();
        AssertObjects(final.Objects, await new RepoWorld(Storage).LoadAsync(Map(1)));
        Assert.Empty(await actor.Ask(new DataWriteWorld(final)));
        await modules.FlushAsync();
        Assert.Equal(2, repo.SaveCount);
    }
    /// <summary>清单提交前真实中断仍保留 Actor 脏标记，后续重试恢复完整新状态。</summary>
    [Fact]
    public async Task InterruptedManifestCommitCanRetryThroughActor()
    {
        var fail = true;
        var repo = new RepoWorld(Storage) { BeforeManifestCommit = () => { if (fail) throw new IOException("模拟提交中断"); } };
        await using var modules = Modules(repo);
        var actor = modules.World(1);
        var value = Object(1);
        await actor.Ask(new DataWriteWorld(new([value])));
        await Assert.ThrowsAsync<IOException>(() => actor.Ask(new DataFlush()));
        Assert.Empty((await new RepoWorld(Storage).LoadAsync(Map(1))).Objects);
        AssertObjects([value], await actor.Ask(new DataReadWorld()));
        fail = false;
        await modules.FlushAsync();
        AssertObjects([value], await new RepoWorld(Storage).LoadAsync(Map(1)));
    }
    /// <summary>地图参与禁用、动态重计时与合并周期；关闭时即使禁用也补写最后内存。</summary>
    [Fact]
    public async Task WorldParticipatesInDynamicPeriodicScheduleAndFinalSave()
    {
        var repo = new DoubleWorldRepo(new RepoWorld(Storage));
        var modules = Modules(repo);
        await using var server = new MockServerService(_env, modules);
        var actor = modules.World(1);
        var value = Object(1);
        await actor.Ask(new DataWriteWorld(new([value])));
        _env.ServerFlushIntervalSeconds = 0;
        server.Tick(100);
        await server.PeriodicFlush;
        Assert.Equal(0, repo.SaveCount);
        Assert.False(Directory.Exists(Storage));
        _env.ServerFlushIntervalSeconds = 10;
        server.Tick(9);
        Assert.Equal(0, repo.SaveCount);
        server.Tick(1);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, repo.SaveCount);
        await actor.Ask(new DataWriteWorld(new([value with { Count = 2 }])));
        server.Tick(9);
        _env.ServerFlushIntervalSeconds = 5;
        server.Tick(4);
        Assert.Equal(1, repo.SaveCount);
        server.Tick(1);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, repo.SaveCount);
        var release = Signal();
        repo.SaveRelease = release.Task;
        repo.SaveEntered = Signal();
        await actor.Ask(new DataWriteWorld(new([value with { Count = 3 }])));
        server.Tick(5);
        await repo.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = server.PeriodicFlush;
        server.Tick(500);
        Assert.Same(current, server.PeriodicFlush);
        release.SetResult();
        await current.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, repo.SaveCount);
        _env.ServerFlushIntervalSeconds = 0;
        await actor.Ask(new DataWriteWorld(new([value with { Count = 4 }])));
        server.Tick(100);
        Assert.Equal(3, repo.SaveCount);
        await server.DisposeAsync();
        Assert.Equal(4, repo.SaveCount);
        Assert.Equal(4, (await new RepoWorld(Storage).LoadAsync(Map(1))).Objects.Single().Count);
        Assert.Equal(ActorState.STOP, actor.State);
    }
    /// <summary>周期中一个地图失败不阻止另一地图和账号保存，诊断可观察，下一周期重试待写地图。</summary>
    [Fact]
    public async Task PeriodicWorldFailureIsIsolatedAndNextCycleRetries()
    {
        var repo = new DoubleWorldRepo(new RepoWorld(Storage)) { FailSave = true };
        var accounts = new RepoAccountRoleSummary(Storage);
        var modules = new DoubleServerModules(_config, accounts, new DoubleServerRepo(), repo,
            _ => ServerPersistenceActorTests.LoginHandlers(), world: _ => Handlers());
        await using var server = new MockServerService(_env, modules);
        var errors = new ConcurrentQueue<Exception>();
        server.Error += errors.Enqueue;
        await modules.World(1).Ask(new DataWriteWorld(new([Object(1)])));
        var second = Object(2);
        await modules.World(2).Ask(new DataWriteWorld(new([second])));
        await modules.Login("alice").Ask(new ServerPersistenceActorTests.DataWriteSummary("独立保存"));
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(errors);
        AssertObjects([second], await new RepoWorld(Storage).LoadAsync(Map(2)));
        Assert.Equal("独立保存", (await accounts.LoadAsync("alice"))[0].Name);
        Assert.Empty((await new RepoWorld(Storage).LoadAsync(Map(1))).Objects);
        repo.FailSave = false;
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single((await new RepoWorld(Storage).LoadAsync(Map(1))).Objects);
        Assert.Equal(3, repo.SaveCount);
    }
    /// <summary>已接纳请求尚未结束时保存必须等待；屏障后枚举能捕获此期间才启动的地图。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitAndClosingBarrierIncludeWorldStartedDuringAcceptedRequest(bool closing)
    {
        var entered = Signal();
        var release = Signal();
        var repo = new RepoWorld(Storage);
        var modules = new DoubleServerModules(_config, new DoubleServerRepo(), new DoubleServerRepo(), repo,
            _ => new ActorHandlers<LoginContext>().Register<Login, LoginRt>(new ServerWriteBackTests.DoubleMutatingLoginHandler(entered, release.Task)),
            world: _ => Handlers());
        var server = new MockServerService(_env, modules);
        var connection = server.OpenConnection();
        var work = server.ReceiveAsync(connection, ServerWriteBackTests.LoginPacket());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var saving = closing ? server.DisposeAsync().AsTask() : server.FlushAsync();
        Assert.False(saving.IsCompleted);
        var value = Object(1);
        await modules.World(1).Ask(new DataWriteWorld(new([value])));
        Assert.False(Directory.Exists(Storage));
        release.SetResult();
        await Task.WhenAll(work, saving).WaitAsync(TimeSpan.FromSeconds(5));
        AssertObjects([value], await new RepoWorld(Storage).LoadAsync(Map(1)));
        await server.DisposeAsync();
    }
    /// <summary>角色组完成才进入地图组，地图组并行且失败仍进入账号组，最终汇总。</summary>
    [Fact]
    public async Task FlushGroupsAreRoleThenParallelWorldThenLoginEvenOnFailure()
    {
        var repo = new DoubleOrderedRepo();
        await using var modules = new DoubleServerModules(_config, repo, repo, repo,
            _ => ServerPersistenceActorTests.LoginHandlers(), _ => ServerPersistenceActorTests.RoleHandlers(), _ => Handlers());
        await modules.Role(10).Ask(new ServerPersistenceActorTests.DataWriteRole(new(new() { Name = "待写角色" }, [], [])));
        await modules.World(1).Ask(new DataWriteWorld(new([Object(1)])));
        await modules.World(2).Ask(new DataWriteWorld(new([Object(2)])));
        await modules.Login("alice").Ask(new ServerPersistenceActorTests.DataWriteSummary("待写账号"));
        var flushing = modules.FlushAsync();
        await repo.RoleEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "role" }, repo.Order.ToArray());
        repo.RoleRelease.SetResult();
        await Task.WhenAll(repo.OneEntered.Task, repo.TwoEntered.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain("login", repo.Order);
        repo.WorldRelease.SetResult();
        var error = await Assert.ThrowsAsync<AggregateException>(() => flushing);
        Assert.Single(error.InnerExceptions);
        Assert.Equal("role", repo.Order.First());
        Assert.Equal("login", repo.Order.Last());
        Assert.Equal(4, repo.Order.Count);
    }
    /// <summary>最终地图保存失败仍保存账号并停止全部 Actor，向关闭调用方报告失败。</summary>
    [Fact]
    public async Task ClosingWorldFailureStillSavesAccountAndStopsAllShards()
    {
        var repo = new DoubleWorldRepo(new RepoWorld(Storage)) { FailSave = true };
        var accounts = new RepoAccountRoleSummary(Storage);
        var modules = new DoubleServerModules(_config, accounts, new DoubleServerRepo(), repo,
            _ => ServerPersistenceActorTests.LoginHandlers(), world: _ => Handlers());
        var server = new MockServerService(_env, modules);
        var world = modules.World(1);
        var login = modules.Login("alice");
        await world.Ask(new DataWriteWorld(new([Object(1)])));
        await login.Ask(new ServerPersistenceActorTests.DataWriteSummary("关闭仍保存"));
        await Assert.ThrowsAsync<AggregateException>(() => server.DisposeAsync().AsTask());
        Assert.Equal(ActorState.STOP, world.State);
        Assert.Equal(ActorState.STOP, login.State);
        Assert.Equal("关闭仍保存", (await accounts.LoadAsync("alice"))[0].Name);
        Assert.True(server.IsDisposed);
        Assert.False(_config.IsDisposed);
        Assert.False(_env.IsDisposed);
    }
    /// <summary>完整装配测试模块，不新增生产业务 Handler。</summary>
    private DoubleServerModules Modules(IWorldRepo repo) => new(_config, new DoubleServerRepo(), new DoubleServerRepo(), repo,
        _ => new(), world: _ => Handlers());
    /// <summary>每个地图使用独立读写注册表。</summary>
    internal static ActorHandlers<WorldContext> Handlers() => new ActorHandlers<WorldContext>()
        .Register<DataReadWorld, DataWorldSnapshot>(new DoubleReadWorldHandler())
        .Register<DataWriteWorld, ImmutableHashSet<DataWorldChunk>>(new DoubleWriteWorldHandler());
    /// <summary>异步测试信号，禁止内联延续重入邮箱。</summary>
    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>只携带不可变快照的测试修改消息。</summary>
    /// <param name="Snapshot">本回合完整候选。</param>
    internal sealed record DataWriteWorld(DataWorldSnapshot Snapshot) : IActorRequest<ImmutableHashSet<DataWorldChunk>>;
    /// <summary>请求当前不可变地图快照。</summary>
    internal sealed record DataReadWorld : IActorRequest<DataWorldSnapshot>;
    /// <summary>只读快照契约的无状态替身。</summary>
    private sealed class DoubleReadWorldHandler : IActorHandler<WorldContext, DataReadWorld, DataWorldSnapshot>
    {
        /// <summary>不暴露可变 Context。</summary>
        public ValueTask<DataWorldSnapshot> HandleAsync(WorldContext context, DataReadWorld request) => ValueTask.FromResult(context.Cache.Snapshot());
    }
    /// <summary>Commit 修改契约的无状态替身，不执行 IO。</summary>
    private sealed class DoubleWriteWorldHandler : IActorHandler<WorldContext, DataWriteWorld, ImmutableHashSet<DataWorldChunk>>
    {
        /// <summary>统一提交入口并返回不可变待写块。</summary>
        public ValueTask<ImmutableHashSet<DataWorldChunk>> HandleAsync(WorldContext context, DataWriteWorld request)
        { context.Cache.Commit(request.Snapshot); return ValueTask.FromResult(context.Cache.DirtyChunks); }
    }
    /// <summary>服务地图加载及邮箱保存契约，真实仓库外包可控屏障与故障。</summary>
    internal sealed class DoubleWorldRepo(IWorldRepo inner) : IWorldRepo
    {
        /// <summary>地图 1 的加载屏障。</summary>
        internal Task? LoadRelease { get; init; }
        /// <summary>地图 1 的保存屏障。</summary>
        internal Task? SaveRelease { get; set; }
        /// <summary>首地图已进入加载。</summary>
        internal TaskCompletionSource LoadEntered { get; } = Signal();
        /// <summary>首地图已进入保存。</summary>
        internal TaskCompletionSource SaveEntered { get; set; } = Signal();
        /// <summary>地图 1 加载故障开关。</summary>
        internal bool FailLoad { get; set; }
        /// <summary>地图 1 保存故障开关。</summary>
        internal bool FailSave { get; set; }
        /// <summary>全部加载调用数。</summary>
        internal int LoadCount;
        /// <summary>全部保存调用数。</summary>
        internal int SaveCount;
        /// <summary>各地图收到的配置规格。</summary>
        internal ConcurrentDictionary<int, DataWorldLayout> Layouts { get; } = new();
        /// <summary>记录规格，首地图可控阻塞，最终完整加载。</summary>
        public async ValueTask<DataWorldSnapshot> LoadAsync(DataWorldLayout layout)
        {
            Interlocked.Increment(ref LoadCount);
            Layouts[layout.MapId] = layout;
            if (layout.MapId == 1)
            {
                LoadEntered.TrySetResult();
                if (LoadRelease is not null) await LoadRelease;
                if (FailLoad) throw new IOException("模拟地图加载失败");
            }
            return await inner.LoadAsync(layout);
        }
        /// <summary>统计调用并为首地图注入保存阻塞或失败。</summary>
        public async ValueTask SaveAsync(DataWorldLayout layout, DataWorldSnapshot snapshot, ImmutableHashSet<DataWorldChunk> dirtyChunks)
        {
            Interlocked.Increment(ref SaveCount);
            if (layout.MapId == 1)
            {
                SaveEntered.TrySetResult();
                if (SaveRelease is not null) await SaveRelease;
                if (FailSave) throw new IOException("模拟地图保存失败");
            }
            await inner.SaveAsync(layout, snapshot, dirtyChunks);
        }
    }
    /// <summary>只服务三组保存顺序契约的可控内存仓库。</summary>
    private sealed class DoubleOrderedRepo : IRoleRepo, IWorldRepo, IAccountRoleSummaryRepo
    {
        /// <summary>保存顺序契约不创建角色。</summary>
        public ValueTask<long> NextRoleIdAsync() => throw new NotSupportedException();
        /// <summary>各组实际进入保存的顺序。</summary>
        internal ConcurrentQueue<string> Order { get; } = new();
        /// <summary>角色保存已进入。</summary>
        internal TaskCompletionSource RoleEntered { get; } = Signal();
        /// <summary>放行角色组。</summary>
        internal TaskCompletionSource RoleRelease { get; } = Signal();
        /// <summary>首地图已进入。</summary>
        internal TaskCompletionSource OneEntered { get; } = Signal();
        /// <summary>次地图已进入。</summary>
        internal TaskCompletionSource TwoEntered { get; } = Signal();
        /// <summary>同时放行地图组。</summary>
        internal TaskCompletionSource WorldRelease { get; } = Signal();
        /// <summary>角色空基线。</summary>
        public ValueTask<DataRoleSnapshot> LoadAsync(long roleId) => ValueTask.FromResult(new DataRoleSnapshot(new(), [], []));
        /// <summary>地图空基线。</summary>
        public ValueTask<DataWorldSnapshot> LoadAsync(DataWorldLayout layout) => ValueTask.FromResult(new DataWorldSnapshot([]));
        /// <summary>账号空基线。</summary>
        public ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username) => ValueTask.FromResult(ImmutableArray<DataRoleSummary>.Empty);
        /// <summary>阻塞角色组，证明地图组尚未进入。</summary>
        public async ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables)
        { Order.Enqueue("role"); RoleEntered.SetResult(); await RoleRelease.Task; }
        /// <summary>两个地图必须并行进入；首地图失败后仍应执行账号组。</summary>
        public async ValueTask SaveAsync(DataWorldLayout layout, DataWorldSnapshot snapshot, ImmutableHashSet<DataWorldChunk> dirtyChunks)
        {
            Order.Enqueue("world" + layout.MapId);
            (layout.MapId == 1 ? OneEntered : TwoEntered).SetResult();
            await WorldRelease.Task;
            if (layout.MapId == 1) throw new IOException("模拟地图组单分片失败");
        }
        /// <summary>记录后续账号组进入。</summary>
        public ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries)
        { Order.Enqueue("login"); return ValueTask.CompletedTask; }
    }
    /// <summary>先释放借用服务，再移除用例自己的目录。</summary>
    public void Dispose() { _config.Dispose(); _env.Dispose(); if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
