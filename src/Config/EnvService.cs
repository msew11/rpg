using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>集中持有本次启动的资源与持久化目录，先于使用目录的服务创建。</summary>
public sealed class EnvService : Service
{
    /// <summary>.env 中跨启动保存控制台日志开关的配置键。</summary>
    public const string ConsoleLogVariable = "RPG_CONSOLE_LOG_ENABLED";
    /// <summary>.env 中持有服务端检测间隔的秒数键，0 关闭检测。</summary>
    public const string ServerFlushIntervalVariable = "RPG_SERVER_FLUSH_INTERVAL_SECONDS";
    /// <summary>缺失或非法配置时的默认检测秒数。</summary>
    private int _serverFlushIntervalSeconds = 5;
    /// <summary>服务端检测秒数，只接受零或正的五秒倍数，写入成功才更新内存。</summary>
    public int ServerFlushIntervalSeconds
    {
        get => _serverFlushIntervalSeconds;
        set
        {
            ThrowIfDisposed();
            if (value < 0 || value % 5 != 0) throw new ArgumentOutOfRangeException(nameof(value), "检测间隔必须为 0 或 5 秒的正整数倍");
            SaveVariable(ServerFlushIntervalVariable, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _serverFlushIntervalSeconds = value;
        }
    }
    /// <summary>用户数据目录中的 .env 路径，未指定路径的隔离测试仅使用内存。</summary>
    public string? EnvFilePath { get; }
    /// <summary>启动时读取、切换成功后更新的日志状态。</summary>
    private bool _consoleLogEnabled;

    /// <summary>是否输出通信与 IO 日志；修改时直接保存 .env，不依赖日志服务。</summary>
    public bool ConsoleLogEnabled
    {
        get => _consoleLogEnabled;
        set
        {
            ThrowIfDisposed();
            SaveVariable(ConsoleLogVariable, value ? "true" : "false");
            _consoleLogEnabled = value;
        }
    }

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
        ProjectSettings.GlobalizePath("user://server"),
        envFilePath: ProjectSettings.GlobalizePath("user://.env")) { }

    /// <summary>覆盖隔离测试目录及 .env 路径；未指定文件时仅使用内存，不依赖 Godot。</summary>
    internal EnvService(string settingsDirectory, string serverDirectory = "",
        string configDirectory = "res://config", string schemaDirectory = "res://schemas",
        string? envFilePath = null)
    {
        EnvFilePath = envFilePath;
        if (envFilePath is not null && File.Exists(envFilePath))
        {
            foreach (var line in File.ReadAllLines(envFilePath))
            {
                if (TryReadVariable(line, ServerFlushIntervalVariable, out var interval))
                    _serverFlushIntervalSeconds = int.TryParse(interval, out var seconds) && seconds >= 0 && seconds % 5 == 0 ? seconds : 5;
                if (!TryReadConsoleLogging(line, out var text)) continue;
                _consoleLogEnabled = text == "1" || bool.TryParse(text, out var enabled) && enabled;
            }
        }
        SettingsDirectory = settingsDirectory;
        ServerDirectory = serverDirectory;
        ConfigDirectory = configDirectory;
        SchemaDirectory = schemaDirectory;
    }

    /// <summary>识别日志配置行，忽略首尾空白并支持成对引号，其他行原样保留。</summary>
    private static bool TryReadConsoleLogging(string line, out string value)
        => TryReadVariable(line, ConsoleLogVariable, out value);

    /// <summary>读取指定键及成对引号内的值，保留其他配置行。</summary>
    private static bool TryReadVariable(string line, string key, out string value)
    {
        var separator = line.IndexOf('=');
        value = "";
        if (separator < 0 || line[..separator].Trim() != key) return false;
        value = line[(separator + 1)..].Trim();
        if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
            value = value[1..^1].Trim();
        return true;
    }

    /// <summary>原子替换 .env，仅更新指定键并合并重复项，整个过程不输出日志。</summary>
    private void SaveVariable(string key, string value)
    {
        if (EnvFilePath is null) return;
        var lines = File.Exists(EnvFilePath) ? File.ReadAllLines(EnvFilePath) : [];
        var updated = new List<string>();
        var replacement = key + "=" + value;
        var found = false;
        foreach (var line in lines)
        {
            if (!TryReadVariable(line, key, out _)) { updated.Add(line); continue; }
            if (!found) updated.Add(replacement);
            found = true;
        }
        if (!found) updated.Add(replacement);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(EnvFilePath))!);
        var temporary = EnvFilePath + ".tmp";
        try
        {
            File.WriteAllLines(temporary, updated);
            File.Move(temporary, EnvFilePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>环境无外部资源，随所有消费者结束生命周期。</summary>
    public override void Dispose() => IsDisposed = true;
}
