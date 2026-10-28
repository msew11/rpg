using System.Collections.Concurrent;
using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Actors;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-actors 的串行、初始化、回调、故障和有序停止。</summary>
public sealed class ServerActorTests
{
    /// <summary>INIT 不调用业务，Tell 与 Ask 共用邮箱，Stop 排空且后续投递失败。</summary>
    [Fact]
    public async Task InitializationOrdersTellAskCallbackAndStop()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var actor = new DoubleActor(ready.Task);
        actor.Tell(2);
        var reply = actor.Ask<int>(3);
        var callback = 0;
        var callbackTask = actor.Ask<int>(4, value => callback = value);
        Assert.Equal(ActorState.INIT, actor.State);
        Assert.False(reply.IsCompleted);
        Assert.Empty(actor.Handled);
        var stopped = actor.StopAsync();
        Assert.Throws<ObjectDisposedException>(() => actor.Tell(5));
        ready.SetResult();
        Assert.Equal(5, await reply.WaitAsync(TimeSpan.FromSeconds(10)));
        await callbackTask.WaitAsync(TimeSpan.FromSeconds(10));
        await stopped.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(9, callback);
        Assert.Equal(new[] { 2, 3, 4 }, actor.Handled);
        Assert.Equal(ActorState.STOP, actor.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => actor.Ask<int>(1));
    }

    /// <summary>多发送者竞争时同一 Actor 不会重入，独立 Actor 不受阻塞邮箱影响。</summary>
    [Fact]
    public async Task ConcurrentSendersSerializeWhileOtherActorContinues()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var blocked = new DoubleActor(ready.Task);
        await using var free = new DoubleActor(Task.CompletedTask);
        var waiting = blocked.Ask<int>(1);
        Assert.Equal(7, await free.Ask<int>(7).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(waiting.IsCompleted);
        ready.SetResult();
        await waiting;
        var responses = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => free.Ask<int>(1))));
        Assert.Equal(71, responses.Max());
        Assert.Equal(1, free.MaxConcurrent);
    }

    /// <summary>初始化失败使全部等待者收到原始诊断，业务及 Tell 失败不终止正常 Actor。</summary>
    [Fact]
    public async Task FailuresPropagateAndBusinessFailureDoesNotStopMailbox()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var failed = new DoubleActor(ready.Task);
        var first = failed.Ask<int>(1);
        var second = failed.Ask<int>(2);
        ready.SetException(new InvalidDataException("初始化存档损坏"));
        Assert.Equal("初始化存档损坏", (await Assert.ThrowsAsync<InvalidDataException>(() => first)).Message);
        await Assert.ThrowsAsync<InvalidDataException>(() => second);
        await failed.StopAsync();
        Assert.Equal(ActorState.STOP, failed.State);
        Assert.Equal("初始化存档损坏", (await Assert.ThrowsAsync<InvalidDataException>(() => failed.Ask<int>(3))).Message);
        Assert.Empty(failed.Handled);

        await using var actor = new DoubleActor(Task.CompletedTask);
        var errors = new ConcurrentQueue<Exception>();
        actor.Error += errors.Enqueue;
        actor.Tell(-1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.Ask<int>(-1));
        Assert.Equal(4, await actor.Ask<int>(4));
        Assert.Single(errors);
        Assert.Equal(ActorState.RUN, actor.State);
    }

    /// <summary>测试替身只服务于邮箱顺序与故障契约，不包含游戏业务。</summary>
    private sealed class DoubleActor(Task initialization) : BaseActor
    {
        /// <summary>已完成处理的消息顺序。</summary>
        internal ConcurrentQueue<int> Handled { get; } = new();
        /// <summary>累计处理值。</summary>
        private int _value;
        /// <summary>当前并发进入处理器的数量。</summary>
        private int _concurrent;
        /// <summary>实际观察到的最高并发。</summary>
        internal int MaxConcurrent { get; private set; }
        /// <summary>初始化屏障保持 INIT 可观察。</summary>
        protected override async ValueTask InitializeAsync() => await initialization.ConfigureAwait(false);
        /// <summary>跨 await 的回合仍不允许下一条消息进入。</summary>
        protected override async ValueTask<object?> ReceiveAsync(object message)
        {
            var value = (int)message;
            if (value < 0) throw new InvalidOperationException("业务拒绝");
            MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref _concurrent));
            await Task.Yield();
            _value += value;
            Handled.Enqueue(value);
            Interlocked.Decrement(ref _concurrent);
            return _value;
        }
    }
}

/// <summary>验证 openspec/specs/server-actors、mock-server 的身份加载和地图共同状态。</summary>
public sealed class ServerActorShardTests : IDisposable
{
    /// <summary>每例独立的存档根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-actors-" + Guid.NewGuid());
    /// <summary>本测试使用的无物品规则。</summary>
    private readonly BagRules _rules = new(8, 5, []);

    /// <summary>角色客户端请求、Ask 和 Tell 均按身份启动，地图被两个角色共享且不重复加载。</summary>
    [Fact]
    public async Task AskTellInitializeOnlyTheirShardAndShareMap()
    {
        var disk = new RepoServerRole(_directory);
        var first = disk.Create("首个角色", ServerRoleGender.Male, 1);
        var second = disk.Create("另一个角色", ServerRoleGender.Female, 1);
        using var server = new MockServer(_directory, _rules);
        var one = server.GetRoleActor(first.Role.Value.RoleId);
        var two = server.GetRoleActor(second.Role.Value.RoleId);
        Assert.Same(one, server.GetRoleActor(first.Role.Value.RoleId));
        Assert.Equal(ActorState.INIT, one.State);
        one.Tell(new DataRoleWork(first.Role.Value.RoleId, cache =>
        {
            cache.Commit(cache.Role.Tables!.WithRole(cache.Role.Tables.Role.Value with { Health = 17 }), 1);
            return true;
        }));
        var reply = await one.Ask<DataRoleResult>(new DataRoleWork(first.Role.Value.RoleId, cache => cache.Role.Tables!.Role.Value.Health));
        Assert.Equal(17, reply.Response);
        Assert.Equal(ActorState.RUN, one.State);
        Assert.Equal(ActorState.INIT, two.State);
        Assert.Equal(ActorState.RUN, server.GetWorldActor(1).State);
        Assert.Same(server.GetWorldActor(1), server.GetWorldActor(1));
        await two.Ask<DataRoleResult>(new DataRoleWork(second.Role.Value.RoleId, cache => cache.Role.Tables!.Role.Value.Health));
        Assert.Equal(100, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
        server.Flush();
        Assert.Equal(17, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
    }

    /// <summary>坏角色初始化失败不终止登录或其他角色，修复后可新建分片重试。</summary>
    [Fact]
    public async Task CorruptRoleDoesNotStopOtherShardsAndCanRetry()
    {
        var disk = new RepoServerRole(_directory);
        var first = disk.Create("故障角色", ServerRoleGender.Male, 1);
        var second = disk.Create("正常角色", ServerRoleGender.Female, 1);
        var path = disk.GetItemPath(first.Role.Value.RoleId);
        var original = File.ReadAllText(path);
        File.WriteAllText(path, "{");
        using var server = new MockServer(_directory, _rules);
        var failed = server.GetRoleActor(first.Role.Value.RoleId);
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.Ask<DataRoleResult>(new DataRoleWork(first.Role.Value.RoleId, _ => true)));
        var other = server.GetRoleActor(second.Role.Value.RoleId);
        await other.Ask<DataRoleResult>(new DataRoleWork(second.Role.Value.RoleId, _ => true));
        Assert.Equal(ActorState.RUN, other.State);
        Assert.Equal("{", File.ReadAllText(path));
        File.WriteAllText(path, original);
        var retried = server.GetRoleActor(first.Role.Value.RoleId);
        Assert.NotSame(failed, retried);
        await retried.Ask<DataRoleResult>(new DataRoleWork(first.Role.Value.RoleId, _ => true));
        Assert.Equal(ActorState.RUN, retried.State);
    }

    /// <summary>mock-server：初始化失败角色仍能只读角色单例完成删除，不重读损坏物品。</summary>
    [Fact]
    public async Task DeletingFailedShardDoesNotReloadCorruptItems()
    {
        var disk = new RepoServerRole(_directory);
        var role = disk.Create("可删除故障", ServerRoleGender.Male, 1);
        File.WriteAllText(disk.GetItemPath(role.Role.Value.RoleId), "{");
        using var server = new MockServer(_directory, _rules);
        var actor = server.GetRoleActor(role.Role.Value.RoleId);
        await Assert.ThrowsAsync<InvalidDataException>(() => actor.Ask<DataRoleResult>(new DataRoleWork(role.Role.Value.RoleId, _ => true)));
        var replies = new List<IMessage>();
        server.ResetSession(1);
        server.Outgoing += packet => replies.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        server.Receive(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        server.Receive(MessageCodec.Encode(new DeleteRole { RoleId = role.Role.Value.RoleId }, 1, true));
        Assert.True(Assert.Single(replies.OfType<DeleteRoleRt>()).Success);
        server.Flush();
        Assert.Empty(disk.ListSummaries());
        Assert.Equal("{", File.ReadAllText(disk.GetItemPath(role.Role.Value.RoleId)));
    }

    /// <summary>server-actors：地图初始化失败仍保留新建角色，修复后可以从磁盘重试选角。</summary>
    [Fact]
    public void FailedMapInitializationKeepsNewRolePending()
    {
        var mapDirectory = Path.Combine(_directory, "maps", "1");
        Directory.CreateDirectory(mapDirectory);
        var manifest = Path.Combine(mapDirectory, "manifest.json");
        File.WriteAllText(manifest, "{");
        using var server = new MockServer(_directory, _rules);
        var replies = new List<IMessage>();
        server.ResetSession(1);
        server.Outgoing += packet => replies.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        void Send(IMessage request) => server.Receive(MessageCodec.Encode(request, 1, true));
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new CreateRole { Name = "保留新角色", HeadId = 1, Gender = RoleGenderPb.Male });
        var id = Assert.Single(replies.OfType<CreateRoleRt>().Last().Roles).RoleId;
        Send(new SelectRole { RoleId = id });
        Assert.False(replies.OfType<SelectRoleRt>().Last().Success);
        server.Flush();
        Assert.Equal(id, Assert.Single(new RepoServerRole(_directory).List()).Role.Value.RoleId);
        Assert.Equal("{", File.ReadAllText(manifest));
        File.Delete(manifest);
        Send(new SelectRole { RoleId = id });
        Assert.True(replies.OfType<SelectRoleRt>().Last().Success);
    }

    /// <summary>世界工作副本和查询快照不泄露可写状态，停止排空全部 Actor。</summary>
    [Fact]
    public async Task WorldSnapshotsRemainStableAndTransportStopsActors()
    {
        var disk = new RepoServerRole(_directory);
        var role = disk.Create("停止角色", ServerRoleGender.Male, 1);
        var server = new MockServer(_directory, _rules);
        using var transport = new MockTransport(server);
        transport.ResetSession(1);
        transport.Send(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        transport.Send(MessageCodec.Encode(new SelectRole { RoleId = role.Role.Value.RoleId }, 1, true));
        var actor = server.GetRoleActor(role.Role.Value.RoleId);
        var world = server.GetWorldActor(1);
        var before = server.GetMap(1);
        var value = server.CreateMapObject(1, new MapObjectEntity { X = 100, Y = 100, MonsterTypeId = "test" });
        Assert.Empty(before.Objects);
        Assert.Equal(value, Assert.Single(server.GetMap(1).Objects).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.Ask<DataRoleResult>(new DataRoleWork(role.Role.Value.RoleId + 1, _ => true)));
        transport.Dispose();
        Assert.Equal(ActorState.STOP, actor.State);
        Assert.Equal(ActorState.STOP, world.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => actor.Ask<DataRoleResult>(new DataRoleWork(role.Role.Value.RoleId, _ => true)));
    }

    /// <summary>只清理本测试独占目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
