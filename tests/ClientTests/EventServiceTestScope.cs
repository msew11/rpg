using Rpg.Services;

// 全局事件域是 session-events 的单实例契约，测试间不得并行替换。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClientTests;

/// <summary>为 session-events 与业务测试持有不注册到全局的独立事件生命周期。</summary>
public abstract class EventServiceTestScope : IDisposable
{
    /// <summary>本测试持有的事件服务，后于方法内的网络和缓存释放。</summary>
    protected EventService Events { get; } = new EventService();
    /// <summary>结束本测试的事件域，清除全部监听与待分发消息。</summary>
    public void Dispose() => Events.Dispose();
}
