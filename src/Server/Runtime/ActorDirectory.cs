using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Rpg.Server.Runtime;

/// <summary>按身份唯一创建 Actor，持有生命周期但仅返回消息引用。</summary>
public sealed class ActorDirectory<TKey> : IAsyncDisposable where TKey : notnull
{
    /// <summary>保护分片创建和目录关闭。</summary>
    private readonly object _gate = new();
    /// <summary>目录拥有的全部分片。</summary>
    private readonly Dictionary<TKey, BaseActor> _actors = new();
    /// <summary>只创建对象，禁止在工厂内启动业务或执行 IO。</summary>
    private readonly Func<TKey, BaseActor> _factory;
    /// <summary>目录已经停止接纳。</summary>
    private Task? _stopping;
    /// <summary>创建分片时统一绑定的诊断渠道。</summary>
    private readonly Action<Exception> _report;

    /// <summary>绑定显式工厂与诊断。</summary>
    public ActorDirectory(Func<TKey, BaseActor> factory, Action<Exception>? report = null)
    { ArgumentNullException.ThrowIfNull(factory); _factory = factory; _report = report ?? (static _ => { }); }

    /// <summary>同一活动身份始终返回同一引用，失败分片可在后续调用重建。</summary>
    public ActorRef Get(TKey key)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping is not null, this);
            if (_actors.TryGetValue(key, out var actor) && actor.State != ActorState.STOP) return actor.Ref;
            actor = _factory(key);
            actor.Error += _report;
            _actors[key] = actor;
            return actor.Ref;
        }
    }
    /// <summary>取得已启动且未失败停止的分片引用，不创建或初始化分片。</summary>
    internal ActorRef[] StartedRefs()
    {
        lock (_gate) return _actors.Values.Where(actor => actor.HasStarted && actor.State != ActorState.STOP)
            .Select(actor => actor.Ref).ToArray();
    }

    /// <summary>阻止新分片并并行排空已有分片；重复调用等待同一任务。</summary>
    public Task StopAsync()
    {
        lock (_gate) return _stopping ??= Task.WhenAll(_actors.Values.Select(actor => actor.StopAsync()));
    }

    /// <summary>目录释放等待全部所属邮箱。</summary>
    public ValueTask DisposeAsync() => new(StopAsync());
}
