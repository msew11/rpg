using Rpg.Config;
using System;
using Rpg.Services;
using Rpg.UI;

namespace Rpg.Tests.Doubles;

/// <summary>为 game-services 和 client-ui 的场景验收提供独立存档环境。</summary>
public partial class DoubleMainController : MainController
{
    /// <summary>本次场景验收的独立设置目录。</summary>
    public string SettingsDirectory { get; set; } = "";
    /// <summary>本次场景验收的独立模拟服务端目录。</summary>
    public string ServerDirectory { get; set; } = "";
    /// <summary>为配置与图标回归提供隔离配置目录，默认使用项目资源。</summary>
    public string ConfigDirectory { get; set; } = "res://config";

    /// <summary>为 console-logging 场景指定独立 .env 文件，不访问实际用户目录。</summary>
    public string? EnvFilePath { get; set; }

    /// <summary>整组服务共享同一隔离环境，节点仅持有释放句柄。</summary>
    protected override IDisposable InitializeServices() => GameServicesTestFactory.Register(
        new EnvService(SettingsDirectory, ServerDirectory, ConfigDirectory,
            envFilePath: EnvFilePath));
}
