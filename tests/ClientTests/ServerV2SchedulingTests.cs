using System.Collections.Concurrent;
using MessageCodec = Rpg.ServerV2.Protocol.MessageCodec;
using Rpg.Config;
using Rpg.Net;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;
using Rpg.Services;

namespace ClientTests;

/// <summary>在 Godot 中验证 openspec/specs/server-v2-storage 的环境依赖、周期、Net 显式保存及关闭。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2SchedulingTests : IDisposable
{
    /// <summary>本例独立服务端目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-v2-scheduling-" + Guid.NewGuid());
    /// <summary>禁用、动态重计时、重叠触发和关闭补写共用一个保存调度。</summary>
    [Fact]
    public async Task PeriodicScheduleResetsOnChangeCoalescesWhileBusyAndFinallyFlushesWhenDisabled()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var repo = new DoubleTrackingRepo(Path.Combine(_directory, "v2"));
        var modules = new ServerModules(repo, new DoubleServerV2Repo(), _ => ServerV2PersistenceActorTests.LoginHandlers());
        await using var server = new MockServerService(env, config, modules);
        var actor = modules.Login("alice");
        await actor.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("第一轮"));
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
        await actor.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("第二轮"));
        server.Tick(9);
        env.ServerFlushIntervalSeconds = 5;
        server.Tick(4);
        Assert.Equal(1, repo.SaveCount);
        server.Tick(1);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, repo.SaveCount);
        var release = ServerV2PersistenceActorTests.Signal();
        repo.SaveRelease = release.Task;
        repo.Entered = ServerV2PersistenceActorTests.Signal();
        await actor.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("第三轮"));
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
        await actor.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("关闭补写"));
        server.Tick(100);
        Assert.Equal(3, repo.SaveCount);
        await server.DisposeAsync();
        Assert.Equal(4, repo.SaveCount);
        Assert.Equal("关闭补写", (await repo.LoadAsync("alice")).Roles[0].Name);
        Assert.False(env.IsDisposed);
        Assert.False(config.IsDisposed);
    }
    /// <summary>周期失败可观察但不终止帧调度，下一周期保存原有待写内存。</summary>
    [Fact]
    public async Task PeriodicFailureIsReportedAndNextTickCanRetry()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var repo = new DoubleTrackingRepo(Path.Combine(_directory, "v2")) { FailWrites = true };
        var modules = new ServerModules(repo, new DoubleServerV2Repo(), _ => ServerV2PersistenceActorTests.LoginHandlers());
        await using var server = new MockServerService(env, config, modules);
        var errors = new ConcurrentQueue<Exception>();
        server.Error += errors.Enqueue;
        await modules.Login("alice").Ask(new ServerV2PersistenceActorTests.DataWriteSummary("等待重试"));
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(errors);
        Assert.Equal(1, repo.SaveCount);
        Assert.False(Directory.Exists(_directory));
        repo.FailWrites = false;
        server.Tick(5);
        await server.PeriodicFlush.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, repo.SaveCount);
        Assert.Equal("等待重试", (await repo.LoadAsync("alice")).Roles[0].Name);
    }
    /// <summary>Net 的现有显式保存入口等待正在处理的请求，而不是只等字节入队。</summary>
    [Fact]
    public async Task NetExplicitFlushWaitsForRequestAndWritesRealAccountFile()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        using var events = new EventBusService();
        using var log = new LogService(Console.WriteLine);
        var entered = ServerV2PersistenceActorTests.Signal();
        var release = ServerV2PersistenceActorTests.Signal();
        var repo = new RepoAccountRoleSummary(Path.Combine(_directory, "v2"));
        var modules = new ServerModules(repo, new DoubleServerV2Repo(), _ => new ActorHandlers<LoginContext>()
            .Register<DataLogin, DataLoginResult>(new ServerV2WriteBackTests.DoubleMutatingLoginHandler(entered, release.Task)));
        using var server = new MockServerService(env, config, modules);
        using var net = new NetService(server, events, log);
        net.Send(new Login { Username = "alice" });
        var flushing = Task.Run(net.FlushServerWrites);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(flushing.IsCompleted);
        Assert.False(Directory.Exists(_directory));
        release.SetResult();
        await flushing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("已接纳修改", (await repo.LoadAsync("alice")).Roles[0].Name);
    }
    /// <summary>默认宿主只加载 V2 子目录，保留登录占位；损坏 V2 文件走统一错误诊断。</summary>
    [Fact]
    public async Task DefaultServiceReadsV2DirectoryAndKeepsNotImplementedLogin()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "alice.role_summary.json"), "旧数据不是有效 JSON");
        var v2 = Path.Combine(_directory, "v2");
        await new RepoAccountRoleSummary(v2).SaveAsync(new("alice", []));
        await using (var server = new MockServerService(env, config))
        {
            var id = server.OpenConnection();
            await server.ReceiveAsync(id, ServerV2WriteBackTests.LoginPacket());
            Assert.True(server.TryReadOutgoing(id, out var packet));
            var response = Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
            Assert.Equal(ErrorCodePb.NotImplemented, response.Code);
        }
        await File.WriteAllTextAsync(Path.Combine(v2, "alice.role_summary.json"), "{");
        await using var broken = new MockServerService(env, config);
        var errors = new ConcurrentQueue<Exception>();
        broken.Error += errors.Enqueue;
        var connection = broken.OpenConnection();
        await broken.ReceiveAsync(connection, ServerV2WriteBackTests.LoginPacket());
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
        var repo = new DoubleTrackingRepo(Path.Combine(_directory, "v2")) { FailWrites = true };
        var modules = new ServerModules(repo, new DoubleServerV2Repo(), _ => ServerV2PersistenceActorTests.LoginHandlers());
        var server = new MockServerService(env, config, modules);
        var actor = modules.Login("alice");
        await actor.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("无法保存"));
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
        /// <summary>可选保存屏障。</summary>
        internal Task? SaveRelease { get; set; }
        /// <summary>下一轮保存进入信号。</summary>
        internal TaskCompletionSource Entered { get; set; } = ServerV2PersistenceActorTests.Signal();
        /// <summary>是否模拟写入故障。</summary>
        internal bool FailWrites { get; set; }
        /// <summary>保存调用次数。</summary>
        internal int SaveCount;
        /// <summary>当前并发保存数。</summary>
        private int _concurrent;
        /// <summary>观察到的最大并发保存数。</summary>
        internal int MaximumConcurrent;
        /// <summary>加载真实文件。</summary>
        public ValueTask<DataAccountRoleSummaries> LoadAsync(string username) => _inner.LoadAsync(username);
        /// <summary>计数后等待屏障，最终转发到真实仓库。</summary>
        public async ValueTask SaveAsync(DataAccountRoleSummaries summaries)
        {
            Interlocked.Increment(ref SaveCount);
            var concurrent = Interlocked.Increment(ref _concurrent);
            MaximumConcurrent = Math.Max(MaximumConcurrent, concurrent);
            try
            {
                Entered.TrySetResult();
                if (SaveRelease is not null) await SaveRelease.ConfigureAwait(false);
                if (FailWrites) throw new IOException("模拟周期写入失败");
                await _inner.SaveAsync(summaries).ConfigureAwait(false);
            }
            finally { Interlocked.Decrement(ref _concurrent); }
        }
    }
    /// <summary>只清理本例创建的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
