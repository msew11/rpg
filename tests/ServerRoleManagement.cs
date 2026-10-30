using System;
using Rpg.Config;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>server-account 的独立手工验收入口，复用真实角色页面且不切换默认游戏。</summary>
public partial class ServerRoleManagement : MainController
{
    /// <summary>自动验收可设置隔离目录；手工启动使用标准用户目录。</summary>
    public string? TestDirectory { get; init; }

    /// <summary>整组借用同一 Env/Config，Net 持有 服务，退出逆序释放。</summary>
    protected override IDisposable InitializeServices() => GameServicesTestFactory.Register(TestDirectory is null
        ? new EnvService() : new EnvService(TestDirectory, System.IO.Path.Combine(TestDirectory, "server"),
            envFilePath: System.IO.Path.Combine(TestDirectory, ".env")));
}
