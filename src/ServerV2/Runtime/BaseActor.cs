using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Rpg.ServerV2.Runtime;

/// <summary>独立任务中的串行邮箱；状态只能在初始化及消息回合内访问。</summary>
public abstract class BaseActor : IAsyncDisposable
{
    /// <summary>所属服务器的运行时资格目录。</summary>
    internal ActorCallRegistry? Calls { get; set; }
    /// <summary>当前串行回合的不可变投递元数据。</summary>
    protected DataActorCall? CurrentCall { get; private set; }
    /// <summary>多发送者、单消费者的有序投递队列。</summary>
    private readonly Channel<Delivery> _mailbox = Channel.CreateUnbounded<Delivery>(new() { SingleReader = true });
    /// <summary>只保护接纳与停止，不围住业务执行。</summary>
    private readonly object _gate = new();
    /// <summary>首次消息启动的唯一消费任务。</summary>
    private Task? _loop;
    /// <summary>是否已经关闭接纳。</summary>
    private bool _closing;
    /// <summary>初始化失败后供后续投递保留的原始错误。</summary>
    private Exception? _failure;
    /// <summary>跨线程观察的当前状态。</summary>
    private int _state;
    /// <summary>初始化、运行或停止状态。</summary>
    public ActorState State => (ActorState)Volatile.Read(ref _state);
    /// <summary>目录保存枚举只选择收到过消息的分片，不触发惰性初始化。</summary>
    internal bool HasStarted { get { lock (_gate) return _loop is not null; } }
    /// <summary>对外唯一消息引用。</summary>
    public ActorRef Ref { get; }
    /// <summary>Tell 或初始化错误的诊断入口，观察者不得改变邮箱结果。</summary>
    public event Action<Exception>? Error;

    /// <summary>构造引用，不在基类构造期间启动子类。</summary>
    protected BaseActor() => Ref = new(this);

    /// <summary>类型化发送请求，调用方延续不占用 Actor 回合。</summary>
    internal async Task<TReply> Ask<TReply>(object request, DataActorCall? call = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new(request, completion, call));
        var result = await completion.Task.ConfigureAwait(false);
        return result is TReply reply ? reply : throw new InvalidOperationException("Actor 回复类型不符");
    }

    /// <summary>通知不创建回复任务。</summary>
    internal void Tell(IActorNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Enqueue(new(notification, null));
    }

    /// <summary>关闭新投递并排空已接纳回合；可重复调用。</summary>
    public Task StopAsync()
    {
        lock (_gate)
        {
            _closing = true;
            _mailbox.Writer.TryComplete();
            if (_loop is null) Volatile.Write(ref _state, (int)ActorState.STOP);
            return _loop ?? Task.CompletedTask;
        }
    }

    /// <summary>异步释放等价于排空邮箱。</summary>
    public ValueTask DisposeAsync() => new(StopAsync());

    /// <summary>在短锁内确定消息接纳顺序并按需启动。</summary>
    private void Enqueue(Delivery delivery)
    {
        lock (_gate)
        {
            if (_closing) throw _failure ?? new ObjectDisposedException(GetType().Name);
            if (!_mailbox.Writer.TryWrite(delivery)) throw new ObjectDisposedException(GetType().Name);
            _loop ??= Task.Run(RunAsync);
        }
    }

    /// <summary>初始化后逐个 await 回合，初始化失败拒绝全部等待者。</summary>
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
                    if (delivery.Call is { } call && Calls is { } registry && !registry.IsCurrent(call)) throw new OperationCanceledException("Actor 投递身份已过期");
                    CurrentCall = delivery.Call;
                    var result = await ReceiveAsync(delivery.Message).ConfigureAwait(false);
                    // 批量结果保留原观察者映射逐个输出校验，不能丢失已分配的序号占位。
                    if (delivery.Call is { Observers: null, PreserveResult: false } completed && Calls is { } current && !current.IsCurrent(completed)) throw new OperationCanceledException("Actor 结果身份已过期");
                    delivery.Completion?.TrySetResult(result);
                }
                catch (Exception error) { Reject(delivery, error); }
                finally { CurrentCall = null; }
            }
        }
        catch (Exception error) { failure = error; Report(error); }
        finally
        {
            lock (_gate)
            {
                _failure = failure;
                _closing = true;
                Volatile.Write(ref _state, (int)ActorState.STOP);
                _mailbox.Writer.TryComplete();
            }
            while (_mailbox.Reader.TryRead(out var pending)) Reject(pending, failure ?? new ObjectDisposedException(GetType().Name));
        }
    }

    /// <summary>请求错误返回等待者，通知错误走独立诊断。</summary>
    private void Reject(Delivery delivery, Exception error)
    {
        if (delivery.Completion is { } completion) completion.TrySetException(error);
        else Report(error);
    }

    /// <summary>逐一隔离观察者，防止诊断失败中断后续消息。</summary>
    private void Report(Exception error)
    {
        if (Error is not { } observers) return;
        foreach (Action<Exception> observer in observers.GetInvocationList())
            try { observer(error); }
            catch (Exception observerError) { System.Diagnostics.Trace.WriteLine(observerError); }
    }

    /// <summary>只在 INIT 构建所属分片的独占状态。</summary>
    protected abstract ValueTask InitializeAsync();
    /// <summary>只在 RUN 串行处理数据消息。</summary>
    protected abstract ValueTask<object?> ReceiveAsync(object message);
    /// <summary>单次投递；通知没有回复句柄。</summary>
    private sealed record Delivery(object Message, TaskCompletionSource<object?>? Completion, DataActorCall? Call = null);
}
