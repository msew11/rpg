namespace Rpg.Server.Runtime;

/// <summary>邮箱初始化、运行与终止的可观察状态。</summary>
public enum ActorState
{
    /// <summary>尚未完成独占状态初始化。</summary>
    INIT,
    /// <summary>正在串行接收消息。</summary>
    RUN,
    /// <summary>终止且不再接纳新消息。</summary>
    STOP
}
