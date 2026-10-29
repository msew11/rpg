using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Common.Data;

/// <summary>由所属 Actor 拦截的保存请求，不携带业务上下文或执行委托。</summary>
internal sealed record DataFlush : IActorRequest<bool>;
