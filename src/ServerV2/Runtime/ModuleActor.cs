using System.Threading.Tasks;
using System;

namespace Rpg.ServerV2.Runtime;

/// <summary>模块 Actor 统一持有自己的上下文及冻结处理器表。</summary>
public abstract class ModuleActor<TContext> : BaseActor
{
    /// <summary>仅本邮箱可以访问的上下文。</summary>
    private TContext _context = default!;
    /// <summary>所属模块的显式消息处理器。</summary>
    private readonly ActorHandlers<TContext> _handlers;
    /// <summary>接管本 Actor 专用处理器，不启动初始化。</summary>
    protected ModuleActor(ActorHandlers<TContext> handlers)
    { ArgumentNullException.ThrowIfNull(handlers); _handlers = handlers; _handlers.Freeze(); }
    /// <summary>首次投递时构造所属分片的独占状态。</summary>
    protected sealed override async ValueTask InitializeAsync() => _context = await CreateContextAsync().ConfigureAwait(false);
    /// <summary>将消息交给对应模块处理器，不向外返回上下文本身。</summary>
    protected sealed override async ValueTask<object?> ReceiveAsync(object message)
    {
        if (_context is Modules.Common.ModuleContext context) context.Call = CurrentCall;
        try { return await DispatchAsync(_context, message).ConfigureAwait(false); }
        finally { if (_context is Modules.Common.ModuleContext completed) completed.Call = null; }
    }
    /// <summary>默认交给处理器表，模块可覆盖持久化等生命周期边界；业务编排留在处理器内。</summary>
    protected virtual ValueTask<object?> DispatchAsync(TContext context, object message) => _handlers.HandleAsync(context, message);
    /// <summary>模块负责初始化状态；后续 IO 加载在此边界接入。</summary>
    protected abstract ValueTask<TContext> CreateContextAsync();
}
