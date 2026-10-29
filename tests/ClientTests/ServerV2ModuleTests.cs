using Rpg.Config;

namespace ClientTests;

/// <summary>为 server-v2-account 模块集成验收创建完整配置依赖，随用例释放，必须在 Godot 宿主执行。</summary>
public abstract class ServerV2ModuleTests : IDisposable
{
    /// <summary>本例配置的环境依赖，不使用全局服务。</summary>
    private readonly EnvService _env = ConfigTestFixture.CreateEnv();
    /// <summary>供模块装配使用的必需配置，测试也不向生产构造传空。</summary>
    protected ConfigService Config { get; }
    /// <summary>在用例开始前加载配置，所有模块共享本例的同一实例。</summary>
    protected ServerV2ModuleTests() => Config = new(_env);
    /// <summary>派生用例先停止模块，再按依赖顺序回收配置与环境。</summary>
    public virtual void Dispose() { Config.Dispose(); _env.Dispose(); }
}
