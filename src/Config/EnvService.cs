using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using Godot;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>集中持有本次启动的资源与持久化目录，先于使用目录的服务创建。</summary>
public sealed class EnvService : Service
{
    /// <summary>.env 中跨启动保存控制台日志开关的配置键。</summary>
    public const string ConsoleLogVariable = "RPG_CONSOLE_LOG_ENABLED";
    /// <summary>.env 中跨启动保存实体锚点显示开关的配置键。</summary>
    public const string ShowEntityAnchorsVariable = "RPG_SHOW_ENTITY_ANCHORS";
    /// <summary>.env 中持有服务端检测间隔的秒数键，0 关闭检测。</summary>
    public const string ServerFlushIntervalVariable = "RPG_SERVER_FLUSH_INTERVAL_SECONDS";
    /// <summary>GM 滚轮缩放开关的持久化键。</summary>
    public const string MouseWheelZoomVariable = "RPG_MOUSE_WHEEL_ZOOM_ENABLED";
    /// <summary>GM 额外显示倍率的持久化键。</summary>
    public const string GmDisplayZoomVariable = "RPG_GM_DISPLAY_ZOOM";
    /// <summary>逻辑 chunk 边缘开关的持久化键。</summary>
    public const string ShowChunkBoundariesVariable = "RPG_SHOW_CHUNK_BOUNDARIES";
    /// <summary>当前 AOI 高亮开关的持久化键。</summary>
    public const string ShowCurrentAoiVariable = "RPG_SHOW_CURRENT_AOI";
    /// <summary>地表绘制区域边缘开关的持久化键。</summary>
    public const string ShowRenderAreaBoundaryVariable = "RPG_SHOW_RENDER_AREA_BOUNDARY";
    /// <summary>实体锚点颜色的持久化键，使用八位 RGBA 十六进制。</summary>
    public const string EntityAnchorColorVariable = "RPG_ENTITY_ANCHOR_COLOR";
    /// <summary>逻辑 chunk 边缘颜色的持久化键。</summary>
    public const string ChunkBoundaryColorVariable = "RPG_CHUNK_BOUNDARY_COLOR";
    /// <summary>地表绘制区域边缘颜色的持久化键。</summary>
    public const string RenderAreaBoundaryColorVariable = "RPG_RENDER_AREA_BOUNDARY_COLOR";
    /// <summary>血条预览百分比的持久化键，none 表示使用实际生命值。</summary>
    public const string HealthPreviewPercentVariable = "RPG_HEALTH_PREVIEW_PERCENT";
    /// <summary>新增 GM 配置的默认值及当前原始值，读取时按对应类型验证。</summary>
    private readonly Dictionary<string, string> _gmSettings = new()
    {
        [MouseWheelZoomVariable] = "false", [GmDisplayZoomVariable] = "1",
        [ShowChunkBoundariesVariable] = "false", [ShowCurrentAoiVariable] = "false",
        [ShowRenderAreaBoundaryVariable] = "false", [EntityAnchorColorVariable] = "ffff00ff",
        [ChunkBoundaryColorVariable] = "00ff00ff", [RenderAreaBoundaryColorVariable] = "ff69b4ff",
        [HealthPreviewPercentVariable] = "none"
    };

    /// <summary>允许滚轮调整世界基础倍率，缺失时关闭。</summary>
    public bool MouseWheelZoomEnabled
    {
        get => ReadBoolean(_gmSettings[MouseWheelZoomVariable]);
        set => SaveGmValue(MouseWheelZoomVariable, value ? "true" : "false");
    }
    /// <summary>额外显示倍率，缺失或非法时使用一倍。</summary>
    public double GmDisplayZoom
    {
        get => double.TryParse(_gmSettings[GmDisplayZoomVariable], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) && value >= 0.1 && value <= 4 ? value : 1;
        set
        {
            if (!double.IsFinite(value) || value < 0.1 || value > 4) throw new ArgumentOutOfRangeException(nameof(value));
            SaveGmValue(GmDisplayZoomVariable, value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
    /// <summary>是否显示逻辑 chunk 边缘，缺失时关闭。</summary>
    public bool ShowChunkBoundaries
    {
        get => ReadBoolean(_gmSettings[ShowChunkBoundariesVariable]);
        set => SaveGmValue(ShowChunkBoundariesVariable, value ? "true" : "false");
    }
    /// <summary>是否高亮当前 AOI，缺失时关闭。</summary>
    public bool ShowCurrentAoi
    {
        get => ReadBoolean(_gmSettings[ShowCurrentAoiVariable]);
        set => SaveGmValue(ShowCurrentAoiVariable, value ? "true" : "false");
    }
    /// <summary>是否显示地表绘制区域边缘，缺失时关闭。</summary>
    public bool ShowRenderAreaBoundary
    {
        get => ReadBoolean(_gmSettings[ShowRenderAreaBoundaryVariable]);
        set => SaveGmValue(ShowRenderAreaBoundaryVariable, value ? "true" : "false");
    }
    /// <summary>锚点颜色，非法时回退黄色。</summary>
    public string EntityAnchorColor
    {
        get => ReadGmColor(EntityAnchorColorVariable, "ffff00ff");
        set => SaveGmColor(EntityAnchorColorVariable, value);
    }
    /// <summary>逻辑 chunk 边缘颜色，非法时回退绿色。</summary>
    public string ChunkBoundaryColor
    {
        get => ReadGmColor(ChunkBoundaryColorVariable, "00ff00ff");
        set => SaveGmColor(ChunkBoundaryColorVariable, value);
    }
    /// <summary>地表绘制区域边缘颜色，非法时回退粉色。</summary>
    public string RenderAreaBoundaryColor
    {
        get => ReadGmColor(RenderAreaBoundaryColorVariable, "ff69b4ff");
        set => SaveGmColor(RenderAreaBoundaryColorVariable, value);
    }
    /// <summary>保存的血条预览百分比，空值使用实际生命值且不改变角色数据。</summary>
    public int? HealthPreviewPercent
    {
        get => int.TryParse(_gmSettings[HealthPreviewPercentVariable], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= 0 && value <= 100 ? value : null;
        set
        {
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
            SaveGmValue(HealthPreviewPercentVariable, value?.ToString(CultureInfo.InvariantCulture) ?? "none");
        }
    }

    /// <summary>复用现有原子写入，成功后才接纳 GM 配置值。</summary>
    private void SaveGmValue(string key, string value)
    {
        ThrowIfDisposed();
        SaveVariable(key, value);
        _gmSettings[key] = value;
    }

    /// <summary>按八位 RGBA 格式读取颜色，不依赖 Godot 原生运行时。</summary>
    private string ReadGmColor(string key, string fallback)
        => IsGmColor(_gmSettings[key]) ? _gmSettings[key].ToLowerInvariant() : fallback;

    /// <summary>颜色只接受八位 RGBA 十六进制，避免保存后无法恢复。</summary>
    private static bool IsGmColor(string? value)
        => value is { Length: 8 } && value.All(Uri.IsHexDigit);

    /// <summary>校验并规范颜色值，再通过相同的 .env 流程保存。</summary>
    private void SaveGmColor(string key, string value)
    {
        if (!IsGmColor(value)) throw new ArgumentException("颜色必须为八位 RGBA 十六进制", nameof(value));
        SaveGmValue(key, value.ToLowerInvariant());
    }
    /// <summary>GM 可调移动速度的下限、上限和默认值。</summary>
    public const int MinimumMovementSpeed = 60, MaximumMovementSpeed = 360, DefaultMovementSpeed = 180;
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
    /// <summary>启动时读取、保存成功后更新的实体锚点显示状态。</summary>
    private bool _showEntityAnchors;

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

    /// <summary>是否显示实体锚点；修改时直接保存 .env。</summary>
    public bool ShowEntityAnchors
    {
        get => _showEntityAnchors;
        set
        {
            ThrowIfDisposed();
            SaveVariable(ShowEntityAnchorsVariable, value ? "true" : "false");
            _showEntityAnchors = value;
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
                if (TryReadVariable(line, ConsoleLogVariable, out var logText))
                    _consoleLogEnabled = ReadBoolean(logText);
                if (TryReadVariable(line, ShowEntityAnchorsVariable, out var anchorText))
                    _showEntityAnchors = ReadBoolean(anchorText);
                foreach (var key in _gmSettings.Keys.ToArray())
                    if (TryReadVariable(line, key, out var setting)) _gmSettings[key] = setting;
            }
        }
        SettingsDirectory = settingsDirectory;
        ServerDirectory = serverDirectory;
        ConfigDirectory = configDirectory;
        SchemaDirectory = schemaDirectory;
    }

    /// <summary>读取布尔配置，支持 true/false 与 1/0，非法值视为关闭。</summary>
    private static bool ReadBoolean(string value)
        => value == "1" || bool.TryParse(value, out var enabled) && enabled;

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
