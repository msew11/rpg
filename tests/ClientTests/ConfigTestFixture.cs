using Rpg.Config;
using Rpg.Settings.Data;

namespace ClientTests;

/// <summary>为 openspec/specs/client-config-map 的地图场景加载真实配置仓库。</summary>
internal static class ConfigTestFixture
{
    /// <summary>载入项目自带的地图配置。</summary>
    internal static ConfigService LoadDefault()
    {
        return new ConfigService(CreateEnv());
    }

    /// <summary>为 game-services 验收提供独立存档目录，配置使用正式资源路径。</summary>
    internal static EnvService CreateEnv(string settingsDirectory = "", string serverDirectory = "")
    {
        return new EnvService(settingsDirectory, serverDirectory);
    }

    /// <summary>从测试程序集目录向上寻找项目根目录。</summary>
    internal static string FindRoot()
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(typeof(ConfigTestFixture).Assembly.Location)!);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
    }
}

/// <summary>按 client-settings 的公开 JSON 存档格式读取验收结果。</summary>
internal static class SettingsTestFixture
{
    /// <summary>读取角色存档；文件尚未创建时使用约定的默认布局。</summary>
    internal static RoleSetting ReadRole(string directory, long roleId)
    {
        var path = Path.Combine(directory, "roles", $"{roleId}.json");
        return File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<RoleSetting>(File.ReadAllText(path))!
            : new RoleSetting();
    }
}
