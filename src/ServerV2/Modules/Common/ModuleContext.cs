using System;
using Rpg.Config;

namespace Rpg.ServerV2.Modules.Common;

/// <summary>模块上下文的共用配置入口，只借用所属服务的配置，不负责释放。</summary>
public abstract class ModuleContext
{
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
    protected ModuleContext(ConfigService config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        _config = config;
    }
}
