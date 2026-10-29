using System;
using Rpg.Config;

namespace Rpg.ServerV2.Modules.Common;

/// <summary>模块上下文的共用配置入口，只借用所属服务的配置，不负责释放。</summary>
public abstract class ModuleContext
{
    /// <summary>正式服务注入的配置；纯内存框架测试可省略，读取时明确拒绝。</summary>
    private readonly ConfigService? _config;
    /// <summary>供 Handler 读取所属服务的配置，释放后不得继续使用。</summary>
    public ConfigService Config
    {
        get
        {
            var config = _config ?? throw new InvalidOperationException("当前模块未装配 ConfigService");
            ObjectDisposedException.ThrowIf(config.IsDisposed, config);
            return config;
        }
    }
    /// <summary>由所属 Actor 构造时传入借用依赖，不访问全局服务。</summary>
    protected ModuleContext(ConfigService? config) => _config = config;
}
