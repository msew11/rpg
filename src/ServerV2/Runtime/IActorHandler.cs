using System.Threading.Tasks;

namespace Rpg.ServerV2.Runtime;

/// <summary>处理独占上下文与请求，不执行 IO；协议请求入队后和回复返回后分别移交使用权。</summary>
public interface IActorHandler<in TContext, in TRequest, TReply>
{
    /// <summary>当前回合的类型化处理入口。</summary>
    ValueTask<TReply> HandleAsync(TContext context, TRequest request);
}

/// <summary>处理独占上下文中的单向数据通知。</summary>
public interface IActorNotificationHandler<in TContext, in TNotification> where TNotification : IActorNotification
{
    /// <summary>当前回合处理通知，不返回业务回复。</summary>
    ValueTask HandleAsync(TContext context, TNotification notification);
}
