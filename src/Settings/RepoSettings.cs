using System;
using System.IO;
using System.Text.Json;

namespace Rpg.Settings;

/// <summary>分别保存系统与角色设置，不与配置或会话缓存混用。</summary>
public sealed class RepoSettings
{
    /// <summary>系统与角色设置的本地根目录。</summary>
    private readonly string _root;
    /// <summary>持久化 JSON 的格式设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>创建基于指定本地目录的设置仓库。</summary>
    public RepoSettings(string root) => _root = root;

    /// <summary>读取系统设置；文件缺失或损坏时采用默认值。</summary>
    public DataSystemSetting LoadSystem()
    {
        var setting = Read(Path.Combine(_root, "system.json"), new DataSystemSetting());
        if (setting.Width < 800 || setting.Width > 3840) setting.Width = 1280;
        if (setting.Height < 600 || setting.Height > 2160) setting.Height = 720;
        if (!float.IsFinite(setting.MasterVolume)) setting.MasterVolume = 0.8f;
        setting.MasterVolume = Math.Clamp(setting.MasterVolume, 0, 1);
        return setting;
    }

    /// <summary>原子保存系统设置。</summary>
    public void SaveSystem(DataSystemSetting setting) => Write(Path.Combine(_root, "system.json"), setting);

    /// <summary>按角色 ID 读取独立设置。</summary>
    public DataRoleSetting LoadRole(long roleId)
    {
        var setting = Read(RolePath(roleId), new DataRoleSetting());
        if (setting.VisibleHotbars is null || setting.VisibleHotbars.Length != 3)
            setting.VisibleHotbars = [true, false, false];
        setting.HudPositions ??= DataRoleSetting.DefaultPositions();
        foreach (var (key, value) in DataRoleSetting.DefaultPositions())
        {
            if (!setting.HudPositions.TryGetValue(key, out var position) || position is null)
                setting.HudPositions[key] = value;
            else
            {
                position.X = float.IsFinite(position.X) ? Math.Clamp(position.X, 0, 1) : value.X;
                position.Y = float.IsFinite(position.Y) ? Math.Clamp(position.Y, 0, 1) : value.Y;
            }
        }
        setting.VisibleHotbars[0] = true;
        return setting;
    }

    /// <summary>按角色 ID 原子保存独立设置。</summary>
    public void SaveRole(long roleId, DataRoleSetting setting) => Write(RolePath(roleId), setting);

    /// <summary>形成只包含数字角色 ID 的文件名。</summary>
    private string RolePath(long roleId) => Path.Combine(_root, "roles", $"{roleId}.json");

    /// <summary>读取 JSON 并在损坏时给出警告。</summary>
    private static T Read<T>(string path, T fallback)
    {
        if (!File.Exists(path)) return fallback;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback; }
        catch (Exception error) when (error is JsonException or IOException)
        {
            Godot.GD.PushWarning($"设置文件 {path} 无法读取，已使用默认值：{error.Message}");
            return fallback;
        }
    }

    /// <summary>通过临时文件替换目标，避免保存中断破坏原文件。</summary>
    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, true);
    }
}
