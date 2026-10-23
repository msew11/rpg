using Rpg.Session;
using Rpg.Session.Cache;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session 的缓存优先监听与统一释放契约。</summary>
public sealed class CacheBaseTests
{
    /// <summary>子类订阅默认先于已有普通监听执行，普通监听可立即读取更新结果。</summary>
    [Fact]
    public void CacheSubscriptionsRunBeforeEarlierOrdinaryListeners()
    {
        using var events = new EventSystem();
        var value = 0;
        var observed = 0;
        using var ordinary = events.Subscribe<int>(_ => observed = value);
        using var cache = new DoubleCache(events);
        cache.Listen<int>(message => value = message);
        events.Publish(42);
        Assert.Equal(42, observed);
    }

    /// <summary>释放取消所有类型订阅且额外清理只执行一次，不影响其他缓存并拒绝重新订阅。</summary>
    [Fact]
    public void DisposeReleasesOwnedSubscriptionsAndRunsCleanupOnce()
    {
        using var events = new EventSystem();
        using var released = new DoubleCache(events);
        using var active = new DoubleCache(events);
        var releasedCalls = 0;
        var activeCalls = 0;
        released.Listen<int>(_ => releasedCalls++);
        released.Listen<string>(_ => releasedCalls++);
        active.Listen<int>(_ => activeCalls++);
        events.Publish(1);
        events.Publish("before");
        Assert.Equal(2, releasedCalls);
        released.Dispose();
        released.Dispose();
        events.Publish(2);
        events.Publish("after");
        Assert.Equal(2, releasedCalls);
        Assert.Equal(2, activeCalls);
        Assert.Equal(1, released.CleanupCalls);
        Assert.Throws<ObjectDisposedException>(() => released.Listen<int>(_ => releasedCalls++));
    }

    /// <summary>提供受控订阅入口与额外清理计数，服务 CacheBase 生命周期契约。</summary>
    private sealed class DoubleCache : CacheBase
    {
        /// <summary>额外清理钩子的实际执行次数。</summary>
        public int CleanupCalls { get; private set; }

        /// <summary>借用测试事件系统建立缓存。</summary>
        public DoubleCache(EventSystem events) : base(events)
        {
        }

        /// <summary>通过基类入口托管测试需要的协议订阅。</summary>
        public void Listen<T>(Action<T> handler) => Subscribe(handler);

        /// <summary>记录基类调用额外清理的次数。</summary>
        protected override void OnDispose() => CleanupCalls++;
    }
}
