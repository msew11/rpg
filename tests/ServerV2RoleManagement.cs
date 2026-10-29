using System;
using Rpg.Config;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>server-v2-account 的独立手工验收入口，复用真实角色页面且不切换默认游戏。</summary>
public partial class ServerV2RoleManagement : MainController
{
    /// <summary>自动验收可设置隔离目录；手工启动使用标准用户目录。</summary>
    public string? TestDirectory { get; init; }

    /// <summary>整组借用同一 Env/Config，Net 持有 V2 服务，退出逆序释放。</summary>
    protected override IDisposable InitializeServices() => GameServicesTestFactory.RegisterV2(TestDirectory is null
        ? new EnvService() : new EnvService(TestDirectory, System.IO.Path.Combine(TestDirectory, "server"),
            envFilePath: System.IO.Path.Combine(TestDirectory, ".env")));
}
