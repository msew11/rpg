namespace Rpg.ServerV2.Runtime;

/// <summary>携带回复类型的数据请求；实现必须使用不可变或独占载荷。</summary>
public interface IActorRequest<TReply> { }

/// <summary>不等待业务回复的数据通知；不得携带操作委托或可写上下文。</summary>
public interface IActorNotification { }
