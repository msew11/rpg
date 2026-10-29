using System.Collections.Concurrent;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-runtime 的顺序、隔离、故障及排空停止。</summary>
public sealed class ServerV2ActorTests
{
    /// <summary>验证 server-v2-runtime：无效请求在接纳入口拒绝，不启动 Actor 或进入 Handler。</summary>
    [Fact]
    public async Task NullRequestIsRejectedBeforeActorInitialization()
    {
        await using var actor = new DoubleActor(Task.CompletedTask);
        await Assert.ThrowsAsync<ArgumentNullException>(() => actor.Ref.Ask<int>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => actor.Ref.Ask(new DataRead(), null!));
        Assert.Throws<ArgumentNullException>(() => actor.Ref.Tell(null!));
        Assert.Equal(ActorState.INIT, actor.State);
        Assert.False(actor.HasStarted);
    }

    /// <summary>初始化屏障前不执行消息，跨 await 仍串行，Ask 回调在调用方执行。</summary>
    [Fact]
    public async Task InitializationSerializesTellAskAndDrainsBeforeStop()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var actor = new DoubleActor(ready.Task);
        actor.Ref.Tell(new DataAdd(2));
        var reply = actor.Ref.Ask(new DataRead());
        var callback = 0;
        var callbackTask = actor.Ref.Ask(new DataRead(), value => callback = value);
        var stopping = actor.StopAsync();
        Assert.False(reply.IsCompleted);
        Assert.Equal(ActorState.INIT, actor.State);
        Assert.Throws<ObjectDisposedException>(() => actor.Ref.Tell(new DataAdd(1)));
        ready.SetResult();
        Assert.Equal(2, await reply.WaitAsync(TimeSpan.FromSeconds(5)));
        await callbackTask;
        await stopping;
        Assert.Equal(2, callback);
        Assert.Equal(ActorState.STOP, actor.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => actor.Ref.Ask(new DataRead()));
    }

    /// <summary>通知和请求失败可观察，初始化失败让全部等待者收到原始错误。</summary>
    [Fact]
    public async Task ErrorsDoNotStrandWaitersOrOtherActors()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var failed = new DoubleActor(ready.Task);
        var one = failed.Ref.Ask(new DataRead());
        var two = failed.Ref.Ask(new DataRead());
        ready.SetException(new InvalidDataException("坏数据"));
        Assert.Equal("坏数据", (await Assert.ThrowsAsync<InvalidDataException>(() => one)).Message);
        await Assert.ThrowsAsync<InvalidDataException>(() => two);
        await failed.StopAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => failed.Ref.Ask(new DataRead()));
        await using var good = new DoubleActor(Task.CompletedTask);
        var errors = new ConcurrentQueue<Exception>();
        good.Error += _ => throw new InvalidOperationException("观察者失败");
        good.Error += errors.Enqueue;
        good.Ref.Tell(new DataAdd(-1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => good.Ref.Ask(new DataRead(true)));
        Assert.Equal(0, await good.Ref.Ask(new DataRead()));
        Assert.Single(errors);
    }

    /// <summary>同键并发获取只创建一个 Actor，未使用分片不初始化，停止后目录拒绝创建。</summary>
    [Fact]
    public async Task DirectoryCreatesOneLazyShardAndAllowsIndependentProgress()
    {
        var count = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var directory = new ActorDirectory<int>(id =>
        { Interlocked.Increment(ref count); return new DoubleActor(id == 1 ? ready.Task : Task.CompletedTask); });
        var refs = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => directory.Get(1))));
        Assert.All(refs, actor => Assert.Same(refs[0], actor));
        Assert.Equal(1, count);
        Assert.Equal(ActorState.INIT, refs[0].State);
        var waiting = refs[0].Ask(new DataRead());
        Assert.Equal(0, await directory.Get(2).Ask(new DataRead()).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(waiting.IsCompleted);
        ready.SetResult();
        await waiting;
        await directory.StopAsync();
        Assert.Throws<ObjectDisposedException>(() => directory.Get(3));
    }

    /// <summary>测试通知只携带增量，不携带操作委托。</summary>
    private sealed record DataAdd(int Value) : IActorNotification;
    /// <summary>测试请求只携带是否模拟失败的标记。</summary>
    private sealed record DataRead(bool Fail = false) : IActorRequest<int>;
    /// <summary>服务于邮箱契约的测试替身，模拟可等待初始化。</summary>
    private sealed class DoubleActor(Task ready) : BaseActor
    {
        /// <summary>邮箱独占的累计值。</summary>
        private int _value;
        /// <summary>等待初始化屏障。</summary>
        protected override async ValueTask InitializeAsync() => await ready.ConfigureAwait(false);
        /// <summary>回合跨越异步延续，仍保持消息顺序。</summary>
        protected override async ValueTask<object?> ReceiveAsync(object message)
        {
            await Task.Yield();
            if (message is DataAdd add)
            { if (add.Value < 0) throw new InvalidOperationException("无效增量"); _value += add.Value; return null; }
            if (message is DataRead { Fail: false }) return _value;
            throw new InvalidOperationException("回合失败");
        }
    }
}
