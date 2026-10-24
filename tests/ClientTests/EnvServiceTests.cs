using Rpg.Config;
using Rpg.Services;

namespace ClientTests;

/// <summary>验证 console-logging 的 .env 读取、跨启动记忆、静默保存和失败恢复。</summary>
public sealed class EnvServiceTests : IDisposable
{
    /// <summary>每个用例独立的环境文件目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-dotenv-" + Guid.NewGuid());
    /// <summary>本用例的 .env 路径。</summary>
    private string EnvPath => Path.Combine(_root, ".env");

    /// <summary>创建带隔离 .env 的环境，不访问系统环境变量。</summary>
    private EnvService Open() => new(_root, envFilePath: EnvPath);

    /// <summary>缺失文件时默认关闭，首次切换才创建文件；开启和关闭均跨实例保留。</summary>
    [Fact]
    public void ToggleRemembersBothStatesWithoutLoggingItsOwnWrite()
    {
        using var first = Open();
        Assert.False(first.ConsoleLogEnabled);
        Assert.False(File.Exists(EnvPath));
        var lines = new List<string>();
        using var log = new LogService(lines.Add, env: first);
        first.ConsoleLogEnabled = true;
        Assert.Equal("RPG_CONSOLE_LOG_ENABLED=true", File.ReadAllText(EnvPath).Trim());
        using var second = Open();
        Assert.True(second.ConsoleLogEnabled);
        second.ConsoleLogEnabled = false;
        using var third = Open();
        Assert.False(third.ConsoleLogEnabled);
        Assert.Equal("RPG_CONSOLE_LOG_ENABLED=false", File.ReadAllText(EnvPath).Trim());
        Assert.Empty(lines);
        Assert.False(File.Exists(EnvPath + ".tmp"));
    }

    /// <summary>解析空白、大小写、0/1 和成对引号；未设置或非法值关闭。</summary>
    [Theory]
    [InlineData("OTHER=true", false)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=", false)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=invalid", false)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=1", true)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=0", false)]
    [InlineData(" RPG_CONSOLE_LOG_ENABLED = TRUE ", true)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=FaLsE", false)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED=\"true\"", true)]
    [InlineData("RPG_CONSOLE_LOG_ENABLED='1'", true)]
    [InlineData("# RPG_CONSOLE_LOG_ENABLED=true", false)]
    public void ReadsLogSetting(string content, bool expected)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(EnvPath, content);
        using var env = Open();
        Assert.Equal(expected, env.ConsoleLogEnabled);
        Assert.Equal(content, File.ReadAllText(EnvPath));
    }

    /// <summary>更新日志键时保留注释、空行及其他配置，重复日志键合并为一条。</summary>
    [Fact]
    public void SavePreservesOtherLinesAndConsolidatesDuplicateKeys()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllLines(EnvPath, ["# 调试设置", "OTHER=keep=unchanged", "",
            "RPG_CONSOLE_LOG_ENABLED=true", "RPG_CONSOLE_LOG_ENABLED=false"]);
        using var env = Open();
        Assert.False(env.ConsoleLogEnabled);
        env.ConsoleLogEnabled = true;
        Assert.Equal(new[] { "# 调试设置", "OTHER=keep=unchanged", "", "RPG_CONSOLE_LOG_ENABLED=true" },
            File.ReadAllLines(EnvPath));
    }

    /// <summary>文件尚无日志键时追加该键，不覆盖其他设置。</summary>
    [Fact]
    public void SaveAppendsMissingKey()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(EnvPath, "OTHER=keep");
        using var env = Open();
        env.ConsoleLogEnabled = true;
        Assert.Equal(new[] { "OTHER=keep", "RPG_CONSOLE_LOG_ENABLED=true" }, File.ReadAllLines(EnvPath));
    }

    /// <summary>写入失败不改变原文件与内存标记，并清理临时文件，异常交由 GM 显示。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedSavePreservesFileAndMemoryState(bool enabled)
    {
        using var env = Open();
        env.ConsoleLogEnabled = enabled;
        var original = File.ReadAllText(EnvPath);
        using (var locked = new FileStream(EnvPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => env.ConsoleLogEnabled = !enabled);
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(enabled, env.ConsoleLogEnabled);
        Assert.Equal(original, File.ReadAllText(EnvPath));
        Assert.False(File.Exists(EnvPath + ".tmp"));
    }

    /// <summary>清理本用例文件，不触碰用户实际的 .env。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
