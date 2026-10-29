using System;
using System.Threading.Tasks;

namespace Rpg.ServerV2.Runtime;

/// <summary>只暴露消息与只读状态，不泄露 Actor 或其上下文。</summary>
public sealed class ActorRef
{
    /// <summary>此引用唯一对应的邮箱。</summary>
    private readonly BaseActor _actor;
    /// <summary>内部绑定已创建邮箱。</summary>
    internal ActorRef(BaseActor actor) => _actor = actor;
    /// <summary>可跨线程观察的生命周期。</summary>
    public ActorState State => _actor.State;
    /// <summary>投递请求并异步取得类型化回复。</summary>
    public Task<TReply> Ask<TReply>(IActorRequest<TReply> request) => _actor.Ask<TReply>(request);
    /// <summary>直接投递独占的协议请求，显式指定回复类型；投递后调用方不再修改请求。</summary>
    public Task<TReply> Ask<TReply>(object request) => _actor.Ask<TReply>(request);
    /// <summary>在调用方延续中执行回复回调，回调不占用邮箱回合。</summary>
    public async Task Ask<TReply>(IActorRequest<TReply> request, Action<TReply> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback(await Ask(request).ConfigureAwait(false));
    }
    /// <summary>向同一有序邮箱发送单向通知。</summary>
    public void Tell(IActorNotification notification) => _actor.Tell(notification);
}
