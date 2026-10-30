using System;
using Rpg.Config;

namespace Rpg.ServerV2.Modules.Common;

/// <summary>模块上下文借用所属服务的配置与分片定位能力，不负责释放。</summary>
public abstract class ModuleContext
{
    /// <summary>当前回合的运行时身份，仅由所属邮箱切换。</summary>
    public Runtime.DataActorCall? Call { get; internal set; }
    /// <summary>只读定位入口；跨 Actor 步骤由 Async Handler 组织，禁止自身 Ask 和循环等待。</summary>
    public IActorLocator Actors { get; }
    /// <summary>构造时已确定的宿主配置，不允许缺失。</summary>
    private readonly ConfigService _config;
    /// <summary>供 Handler 读取所属服务的配置，释放后不得继续使用。</summary>
    public ConfigService Config
    {
        get
        {
            ObjectDisposedException.ThrowIf(_config.IsDisposed, _config);
            return _config;
        }
    }
    /// <summary>由所属 Actor 构造时传入借用依赖，不访问全局服务。</summary>
    protected ModuleContext(ConfigService config, IActorLocator actors)
    {
        ArgumentNullException.ThrowIfNull(config);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        ArgumentNullException.ThrowIfNull(actors);
        _config = config;
        Actors = actors;
    }
}
