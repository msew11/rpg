using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Rpg.Server.Actors;

/// <summary>Actor 的初始化、运行和终止状态。</summary>
public enum ActorState
{
    /// <summary>尚未完成所属数据加载。</summary>
    INIT,
    /// <summary>可以串行处理业务消息。</summary>
    RUN,
    /// <summary>邮箱已经排空或初始化失败，拒绝后续投递。</summary>
    STOP
}

/// <summary>独立异步任务中的串行邮箱；子类仅在邮箱回合内访问业务状态。</summary>
public abstract class BaseActor : IAsyncDisposable
{
    /// <summary>多发送者、单消费者的有序邮箱。</summary>
    private readonly Channel<Delivery> _mailbox = Channel.CreateUnbounded<Delivery>(new() { SingleReader = true });
    /// <summary>只保护启动和停止接纳，不包围业务操作。</summary>
    private readonly object _lifecycle = new();
    /// <summary>唯一邮箱消费任务。</summary>
    private Task? _loop;
    /// <summary>已经关闭新消息入口。</summary>
    private bool _closing;
    /// <summary>初始化失败的原始诊断，后续投递仍返回同一原因。</summary>
    private Exception? _failure;
    /// <summary>可跨线程观察的生命周期。</summary>
    private int _state = (int)ActorState.INIT;
    /// <summary>当前状态；STOP 是终态。</summary>
    public ActorState State => (ActorState)Volatile.Read(ref _state);
    /// <summary>无回复通知发生错误时的诊断渠道。</summary>
    public event Action<Exception>? Error;

    /// <summary>发送请求，结果或错误通过任务返回，延续不占用 Actor 回合。</summary>
    public async Task<T> Ask<T>(object message)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new(message, completion));
        var result = await completion.Task.ConfigureAwait(false);
        return result is T value ? value : throw new InvalidOperationException("Actor 回复类型不符");
    }

    /// <summary>等待结果后在调用方延续中执行回调；回调异常由返回任务报告。</summary>
    public async Task Ask<T>(object message, Action<T> callback) => callback(await Ask<T>(message).ConfigureAwait(false));

    /// <summary>投递单向通知；不建立业务回复任务。</summary>
    public void Tell(object message) => Enqueue(new(message, null));

    /// <summary>终止接纳并排空已接纳回合；可重复调用。</summary>
    public Task StopAsync()
    {
        lock (_lifecycle)
        {
            _closing = true;
            _mailbox.Writer.TryComplete();
            if (_loop is null) Volatile.Write(ref _state, (int)ActorState.STOP);
            return _loop ?? Task.CompletedTask;
        }
    }

    /// <summary>异步释放等价于有序停止。</summary>
    public ValueTask DisposeAsync() => new(StopAsync());

    /// <summary>首次投递后才启动任务，避免基类构造期间访问子类。</summary>
    private void Enqueue(Delivery delivery)
    {
        lock (_lifecycle)
        {
            if (_closing || !_mailbox.Writer.TryWrite(delivery)) throw _failure ?? new ObjectDisposedException(GetType().Name);
            _loop ??= Task.Run(RunAsync);
        }
    }

    /// <summary>先完成初始化，再逐条处理消息；初始化失败拒绝全部等待者。</summary>
    private async Task RunAsync()
    {
        Exception? failure = null;
        try
        {
            await InitializeAsync().ConfigureAwait(false);
            Volatile.Write(ref _state, (int)ActorState.RUN);
            await foreach (var delivery in _mailbox.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    var reply = await ReceiveAsync(delivery.Message).ConfigureAwait(false);
                    delivery.Completion?.TrySetResult(reply);
                }
                catch (Exception error) { Reject(delivery, error); }
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            lock (_lifecycle)
            {
                _closing = true;
                _failure = failure;
                Volatile.Write(ref _state, (int)ActorState.STOP);
                _mailbox.Writer.TryComplete();
            }
            while (_mailbox.Reader.TryRead(out var pending)) Reject(pending, failure ?? new ObjectDisposedException(GetType().Name));
        }
    }

    /// <summary>Ask 完成失败，Tell 通过诊断事件报告且不终止其他回合。</summary>
    private void Reject(Delivery delivery, Exception error)
    {
        if (delivery.Completion is { } completion) completion.TrySetException(error);
        else
            try { Error?.Invoke(error); }
            catch (Exception observer) { System.Diagnostics.Trace.WriteLine(observer); }
    }

    /// <summary>在 INIT 状态加载本分片，禁止在构造中启动业务。</summary>
    protected abstract ValueTask InitializeAsync();
    /// <summary>仅在 RUN 状态处理消息。</summary>
    protected abstract ValueTask<object?> ReceiveAsync(object message);

    /// <summary>邮箱的一次投递；Tell 没有回复句柄。</summary>
    private sealed record Delivery(object Message, TaskCompletionSource<object?>? Completion);
}
