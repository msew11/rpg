using System.Collections.Immutable;
using System.Collections.Concurrent;
using MessageCodec = Rpg.Server.Protocol.MessageCodec;
using Rpg.Config;
using Rpg.Net;
using Rpg.Protocol;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;
using Rpg.Services;

namespace ClientTests;

/// <summary>在 Godot 中验证 openspec/specs/server-storage 的环境依赖、周期、Net 显式保存及关闭。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerSchedulingTests : IDisposable
{
    /// <summary>本例独立服务端目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-scheduling-" + Guid.NewGuid());
    /// <summary>禁用、动态重计时、重叠触发和关闭补写共用一个保存调度。</summary>
    [Fact]
    public async Task PeriodicScheduleResetsOnChangeCoalescesWhileBusyAndFinallyFlushesWhenDisabled()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var repo = new DoubleTrackingRepo(Path.GetFullPath(_directory));
        var modules = new DoubleServerModules(config, repo, new DoubleServerRepo(), new DoubleServerRepo(), _ => ServerPersistenceActorTests.LoginHandlers());
        await using var server = new MockServerService(env, modules);
        var actor = modules.Login("alice");
        await actor.Ask(new ServerPersistenceActorTests.DataWriteSummary("第一轮"));
        env.ServerFlushIntervalSeconds = 0;
        server.Tick(100);
        await server.PeriodicFlush;
        Assert.Equal(0, repo.SaveCount);
        env.ServerFlushIntervalSeconds = 10;
        server.Tick(9);
        Assert.Equal(0, repo.SaveCount);
        server.Tick(1);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, repo.SaveCount);
        await actor.Ask(new ServerPersistenceActorTests.DataWriteSummary("第二轮"));
        server.Tick(9);
        env.ServerFlushIntervalSeconds = 5;
        server.Tick(4);
        Assert.Equal(1, repo.SaveCount);
        server.Tick(1);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, repo.SaveCount);
        var release = ServerPersistenceActorTests.Signal();
        repo.SaveRelease = release.Task;
        repo.Entered = ServerPersistenceActorTests.Signal();
        await actor.Ask(new ServerPersistenceActorTests.DataWriteSummary("第三轮"));
        server.Tick(5);
        await repo.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = server.PeriodicFlush;
        server.Tick(500);
        Assert.Same(current, server.PeriodicFlush);
        Assert.Equal(3, repo.SaveCount);
        release.SetResult();
        await current.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, repo.MaximumConcurrent);
        env.ServerFlushIntervalSeconds = 0;
        await actor.Ask(new ServerPersistenceActorTests.DataWriteSummary("关闭补写"));
        server.Tick(100);
        Assert.Equal(3, repo.SaveCount);
        await server.DisposeAsync();
        Assert.Equal(4, repo.SaveCount);
        Assert.Equal("关闭补写", (await repo.LoadAsync("alice"))[0].Name);
        Assert.False(env.IsDisposed);
        Assert.False(config.IsDisposed);
    }
    /// <summary>周期失败可观察但不终止帧调度，下一周期保存原有待写内存。</summary>
    [Fact]
    public async Task PeriodicFailureIsReportedAndNextTickCanRetry()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var repo = new DoubleTrackingRepo(Path.GetFullPath(_directory)) { FailWrites = true };
        var modules = new DoubleServerModules(config, repo, new DoubleServerRepo(), new DoubleServerRepo(), _ => ServerPersistenceActorTests.LoginHandlers());
        await using var server = new MockServerService(env, modules);
        var errors = new ConcurrentQueue<Exception>();
        server.Error += errors.Enqueue;
        await modules.Login("alice").Ask(new ServerPersistenceActorTests.DataWriteSummary("等待重试"));
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(errors);
        Assert.Equal(1, repo.SaveCount);
        Assert.False(Directory.Exists(_directory));
        repo.FailWrites = false;
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, repo.SaveCount);
        Assert.Equal("等待重试", (await repo.LoadAsync("alice"))[0].Name);
    }
    /// <summary>Net 的现有显式保存入口等待正在处理的请求，而不是只等字节入队。</summary>
    [Fact]
    public async Task NetExplicitFlushWaitsForRequestAndWritesRealAccountFile()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        using var events = new EventBusService();
        using var log = new LogService(Console.WriteLine);
        var entered = ServerPersistenceActorTests.Signal();
        var release = ServerPersistenceActorTests.Signal();
        var repo = new RepoAccountRoleSummary(Path.GetFullPath(_directory));
        var modules = new DoubleServerModules(config, repo, new DoubleServerRepo(), new DoubleServerRepo(), _ => new ActorHandlers<LoginContext>()
            .Register<Login, LoginRt>(new ServerWriteBackTests.DoubleMutatingLoginHandler(entered, release.Task)));
        using var server = new MockServerService(env, modules);
        using var net = NetService.CreateInProcess(server, events, log);
        net.Send(new Login { Username = "alice" });
        var flushing = Task.Run(net.FlushServerWrites);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(flushing.IsCompleted);
        Assert.False(Directory.Exists(_directory));
        release.SetResult();
        await flushing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("已接纳修改", (await repo.LoadAsync("alice"))[0].Name);
    }
    /// <summary>默认宿主只加载 子目录，错误凭据被拒绝；损坏 文件走统一错误诊断。</summary>
    [Fact]
    public async Task DefaultServiceReadsStorageDirectoryAndRejectsInvalidCredentials()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "alice.role_summary.json"), "旧数据不是有效 JSON");
        var message = Path.GetFullPath(_directory);
        await new RepoAccountRoleSummary(message).SaveAsync("alice", []);
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection();
            await server.ReceiveAsync(id, ServerWriteBackTests.LoginPacket());
            Assert.True(server.TryReadOutgoing(id, out var packet));
            var response = Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
            Assert.Equal(ErrorCodePb.InvalidCredentials, response.Code);
        }
        await File.WriteAllTextAsync(Path.Combine(message, "alice.role_summary.json"), "{");
        await using var broken = MockServerService.CreateInProcess(env, config);
        var errors = new ConcurrentQueue<Exception>();
        broken.Error += errors.Enqueue;
        var connection = broken.OpenConnection();
        await broken.ReceiveAsync(connection, ServerWriteBackTests.LoginPacket());
        Assert.True(broken.TryReadOutgoing(connection, out var bytes));
        Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false)).Code);
        Assert.NotEmpty(errors);
    }
    /// <summary>关闭保存失败仍停止资源，且不释放借用的环境和配置。</summary>
    [Fact]
    public async Task DisposeReportsSaveFailureButReleasesServerOnly()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var repo = new DoubleTrackingRepo(Path.GetFullPath(_directory)) { FailWrites = true };
        var modules = new DoubleServerModules(config, repo, new DoubleServerRepo(), new DoubleServerRepo(), _ => ServerPersistenceActorTests.LoginHandlers());
        var server = new MockServerService(env, modules);
        var actor = modules.Login("alice");
        await actor.Ask(new ServerPersistenceActorTests.DataWriteSummary("无法保存"));
        await Assert.ThrowsAsync<AggregateException>(async () => await server.DisposeAsync());
        Assert.True(server.IsDisposed);
        Assert.Equal(ActorState.STOP, actor.State);
        Assert.False(env.IsDisposed);
        Assert.False(config.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => { _ = server.FlushAsync(); });
    }
    /// <summary>带计数和可控故障的真实摘要仓库装饰器，验证调度契约。</summary>
    private sealed class DoubleTrackingRepo(string directory) : IAccountRoleSummaryRepo
    {
        /// <summary>真正执行文件读写的仓库。</summary>
        private readonly RepoAccountRoleSummary _inner = new(directory);
        /// <summary>取号复用真实仓库的共享序列。</summary>
        public ValueTask<long> NextRoleIdAsync() => _inner.NextRoleIdAsync();
        /// <summary>可选保存屏障。</summary>
        internal Task? SaveRelease { get; set; }
        /// <summary>下一轮保存进入信号。</summary>
        internal TaskCompletionSource Entered { get; set; } = ServerPersistenceActorTests.Signal();
        /// <summary>是否模拟写入故障。</summary>
        internal bool FailWrites { get; set; }
        /// <summary>保存调用次数。</summary>
        internal int SaveCount;
        /// <summary>当前并发保存数。</summary>
        private int _concurrent;
        /// <summary>观察到的最大并发保存数。</summary>
        internal int MaximumConcurrent;
        /// <summary>加载真实文件。</summary>
        public ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username) => _inner.LoadAsync(username);
        /// <summary>计数后等待屏障，最终转发到真实仓库。</summary>
        public async ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries)
        {
            Interlocked.Increment(ref SaveCount);
            var concurrent = Interlocked.Increment(ref _concurrent);
            MaximumConcurrent = Math.Max(MaximumConcurrent, concurrent);
            try
            {
                Entered.TrySetResult();
                if (SaveRelease is not null) await SaveRelease.ConfigureAwait(false);
                if (FailWrites) throw new IOException("模拟周期写入失败");
                await _inner.SaveAsync(username, summaries).ConfigureAwait(false);
            }
            finally { Interlocked.Decrement(ref _concurrent); }
        }
    }
    /// <summary>只清理本例创建的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
