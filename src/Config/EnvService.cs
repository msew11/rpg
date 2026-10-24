using Godot;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>集中持有本次启动的资源与持久化目录，先于使用目录的服务创建。</summary>
public sealed class EnvService : Service
{
    /// <summary>本次运行是否输出通信与 IO 控制台日志，默认关闭且不持久化。</summary>
    public bool ConsoleLogEnabled { get; set; }

    /// <summary>打包或独立的客户端配置目录。</summary>
    public string ConfigDirectory { get; }
    /// <summary>与配置配套的 JSON Schema 目录。</summary>
    public string SchemaDirectory { get; }
    /// <summary>系统与角色设置的持久化目录。</summary>
    public string SettingsDirectory { get; }
    /// <summary>进程内模拟服务端的存档目录。</summary>
    public string ServerDirectory { get; }

    /// <summary>使用随程序发布的资源和当前用户的持久化目录。</summary>
    public EnvService() : this(ProjectSettings.GlobalizePath("user://settings"),
        ProjectSettings.GlobalizePath("user://server")) { }

    /// <summary>覆盖隔离测试的目录；普通磁盘路径不依赖 Godot 运行环境。</summary>
    internal EnvService(string settingsDirectory, string serverDirectory = "",
        string configDirectory = "res://config", string schemaDirectory = "res://schemas")
    {
        SettingsDirectory = settingsDirectory;
        ServerDirectory = serverDirectory;
        ConfigDirectory = configDirectory;
        SchemaDirectory = schemaDirectory;
    }

    /// <summary>环境无外部资源，随所有消费者结束生命周期。</summary>
    public override void Dispose() => IsDisposed = true;
}
