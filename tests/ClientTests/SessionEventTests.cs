using Rpg.Services;
using Rpg.Net;

namespace ClientTests;

/// <summary>验证 openspec/specs/session-events 的优先级、生命周期、重入与异常场景。</summary>
public sealed class SessionEventTests : EventBusServiceTestScope
{
    /// <summary>先注册的 UI 也能读到所有缓存监听处理后的值。</summary>
    [Fact]
    public void AllCachesRunBeforeEarlierRegisteredUi()
    {
        var eventBus = EventBus;
        var order = new List<string>();
        var value = 0;
        using var ui = eventBus.Subscribe<int>(_ => { Assert.Equal(2, value); order.Add("ui"); });
        using var first = eventBus.Subscribe<int>(_ => { value++; order.Add("cache1"); }, EventPhase.Cache);
        using var second = eventBus.Subscribe<int>(_ => { value++; order.Add("cache2"); }, EventPhase.Cache);
        eventBus.Publish(1);
        Assert.Equal(new[] { "cache1", "cache2", "ui" }, order);
    }

    /// <summary>快照不包含新监听，并会跳过分发中取消的旧监听。</summary>
    [Fact]
    public void RegistrationAndDisposalDuringDispatchRespectSnapshot()
    {
        var eventBus = EventBus;
        IDisposable? removed = null;
        IDisposable? added = null;
        var order = new List<string>();
        using var first = eventBus.Subscribe<int>(_ =>
        {
            order.Add("first");
            removed!.Dispose();
            added ??= eventBus.Subscribe<int>(_ => order.Add("added"));
        });
        removed = eventBus.Subscribe<int>(_ => order.Add("removed"));
        eventBus.Publish(1);
        Assert.Equal(new[] { "first" }, order);
        eventBus.Publish(2);
        Assert.Equal(new[] { "first", "first", "added" }, order);
        added?.Dispose();
        added?.Dispose();
    }

    /// <summary>缓存回调的嵌套发布不会抢在当前 UI 前执行。</summary>
    [Fact]
    public void ReentrantMessagesWaitForCurrentNormalPhase()
    {
        var eventBus = EventBus;
        var order = new List<string>();
        using var cache = eventBus.Subscribe<int>(value =>
        {
            order.Add($"cache{value}");
            if (value == 1) eventBus.Publish(2);
        }, EventPhase.Cache);
        using var ui = eventBus.Subscribe<int>(value => order.Add($"ui{value}"));
        eventBus.Publish(1);
        Assert.Equal(new[] { "cache1", "ui1", "cache2", "ui2" }, order);
    }

    /// <summary>缓存异常跳过 UI，普通异常不阻断其他监听或后续消息。</summary>
    [Fact]
    public void ListenerFailuresAreIsolatedByPhase()
    {
        var eventBus = EventBus;
        var errors = 0;
        var calls = 0;
        eventBus.Error += _ => errors++;
        eventBus.Error += _ => throw new InvalidOperationException("错误观察者异常");
        using var cache = eventBus.Subscribe<int>(value => { if (value == 1) throw new InvalidOperationException("缓存失败"); }, EventPhase.Cache);
        using var badUi = eventBus.Subscribe<int>(_ => throw new InvalidOperationException("UI 失败"));
        using var goodUi = eventBus.Subscribe<int>(_ => calls++);
        eventBus.Publish(1);
        Assert.Equal(0, calls);
        eventBus.Publish(2);
        Assert.Equal(1, calls);
        Assert.Equal(2, errors);
    }

    /// <summary>会话重置中止旧消息剩余监听并清理嵌套消息。</summary>
    [Fact]
    public void ResetDuringDispatchInvalidatesCurrentSnapshotAndQueue()
    {
        var eventBus = EventBus;
        var calls = 0;
        using var cache = eventBus.Subscribe<int>(_ => { eventBus.Publish("old"); eventBus.ResetPending(); }, EventPhase.Cache);
        using var ui = eventBus.Subscribe<int>(_ => calls++);
        using var nested = eventBus.Subscribe<string>(_ => calls++);
        eventBus.Publish(1);
        Assert.Equal(0, calls);
    }
}

/// <summary>服务于 client-network 字节边界契约的可控传输替身。</summary>
internal sealed class DoubleNetTransport : INetTransport
{
    /// <summary>捕获发送方冻结的字节。</summary>
    public List<byte[]> Sent { get; } = [];
    /// <summary>模拟传输发送失败，服务于通信日志的失败隔离契约。</summary>
    public bool FailSend { get; init; }
    /// <summary>向网络服务提交测试字节。</summary>
    public event Action<byte[]>? Received;
    /// <summary>记录发送数据，不执行任何业务逻辑。</summary>
    public void Send(byte[] packet)
    {
        if (FailSend) throw new IOException("模拟发送失败");
        Sent.Add((byte[])packet.Clone());
    }
    /// <summary>此替身不需要时间推进。</summary>
    public void Tick(double delta) { }
    /// <summary>保留已发送记录供断言，代次由信封验证。</summary>
    public void ResetSession(long generation) { }
    /// <summary>模拟在途服务器信封到达。</summary>
    public void Inject(byte[] packet) => Received?.Invoke(packet);
    /// <summary>释放接收订阅。</summary>
    public void Dispose() => Received = null;
}
