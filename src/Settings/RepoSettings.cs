using System;
using System.IO;
using System.Text.Json;
using Rpg.Settings.Data;

namespace Rpg.Settings;

/// <summary>分别保存系统与角色设置，不与配置或会话缓存混用。</summary>
public sealed class RepoSettings
{
    /// <summary>系统与角色设置的本地根目录。</summary>
    private readonly string _root;
    /// <summary>当前系统显示和声音设置。</summary>
    private readonly SystemSetting _systemSetting;
    /// <summary>当前选中角色的设置。</summary>
    private RoleSetting? _roleSetting;
    /// <summary>当前选中角色的标识。</summary>
    private long? _roleId;
    /// <summary>当前角色设置是否有未保存的修改。</summary>
    private bool _roleDirty;
    /// <summary>持久化 JSON 的格式设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>创建基于指定本地目录的设置仓库。</summary>
    public RepoSettings(string root)
    {
        _root = root;
        _systemSetting = LoadSystem();
    }

    /// <summary>当前窗口宽度。</summary>
    public int Width => _systemSetting.Width;
    /// <summary>当前窗口高度。</summary>
    public int Height => _systemSetting.Height;
    /// <summary>当前主音量。</summary>
    public float MasterVolume => _systemSetting.MasterVolume;
    /// <summary>切换当前角色并载入其独立设置。</summary>
    public void SelectRole(long roleId)
    {
        SaveCurrentRole();
        _roleSetting = LoadRole(roleId);
        _roleId = roleId;
        _roleDirty = false;
    }

    /// <summary>修改并保存当前分辨率。</summary>
    public void SetResolution(int width, int height)
    {
        _systemSetting.Width = width;
        _systemSetting.Height = height;
        SaveSystem(_systemSetting);
    }

    /// <summary>修改并保存当前主音量。</summary>
    public void SetMasterVolume(float volume)
    {
        _systemSetting.MasterVolume = Math.Clamp(volume, 0, 1);
        SaveSystem(_systemSetting);
    }

    /// <summary>读取当前角色的技能栏显隐状态。</summary>
    public bool IsHotbarVisible(int index) => CurrentRole.VisibleHotbars[index - 1];

    /// <summary>修改并保存当前角色的技能栏显隐状态。</summary>
    public void SetHotbarVisible(int index, bool visible)
    {
        if (index == 1 && !visible) throw new ArgumentException("第一条技能栏始终显示", nameof(visible));
        CurrentRole.VisibleHotbars[index - 1] = visible;
        _roleDirty = true;
        SaveCurrentRole();
    }

    /// <summary>读取当前角色 HUD 元素位置的副本。</summary>
    public HudPosition GetHudPosition(string key)
    {
        var position = CurrentRole.HudPositions[key];
        return new HudPosition(position.X, position.Y);
    }

    /// <summary>更新当前角色的 HUD 位置，提交时再保存。</summary>
    public void SetHudPosition(string key, float x, float y)
    {
        CurrentRole.HudPositions[key] = new HudPosition(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        _roleDirty = true;
    }

    /// <summary>恢复并保存当前角色的默认 HUD 布局。</summary>
    public void ResetHudPositions()
    {
        CurrentRole.HudPositions = RoleSetting.DefaultPositions();
        _roleDirty = true;
        SaveCurrentRole();
    }

    /// <summary>保存当前角色的设置。</summary>
    public void SaveCurrentRole()
    {
        if (!_roleDirty || _roleId is not long roleId) return;
        SaveRole(roleId, CurrentRole);
        _roleDirty = false;
    }

    /// <summary>需要先选中角色才能访问的当前角色设置。</summary>
    private RoleSetting CurrentRole => _roleSetting ?? throw new InvalidOperationException("尚未选择角色");

    /// <summary>系统设置文件的绝对路径。</summary>
    public string SystemPath => Path.GetFullPath(Path.Combine(_root, "system.json"));

    /// <summary>指定角色设置文件的绝对路径。</summary>
    public string GetRolePath(long roleId) => Path.GetFullPath(Path.Combine(_root, "roles", $"{roleId}.json"));

    /// <summary>读取系统设置；文件缺失或损坏时采用默认值。</summary>
    public SystemSetting LoadSystem()
    {
        var setting = Read(SystemPath, new SystemSetting());
        if (setting.Width < 800 || setting.Width > 3840) setting.Width = 1280;
        if (setting.Height < 600 || setting.Height > 2160) setting.Height = 720;
        if (!float.IsFinite(setting.MasterVolume)) setting.MasterVolume = 0.8f;
        setting.MasterVolume = Math.Clamp(setting.MasterVolume, 0, 1);
        return setting;
    }

    /// <summary>原子保存系统设置。</summary>
    public void SaveSystem(SystemSetting setting) => Write(SystemPath, setting);

    /// <summary>按角色 ID 读取独立设置。</summary>
    public RoleSetting LoadRole(long roleId)
    {
        var setting = Read(GetRolePath(roleId), new RoleSetting());
        if (setting.VisibleHotbars is null || setting.VisibleHotbars.Length != 3)
            setting.VisibleHotbars = [true, false, false];
        setting.HudPositions ??= RoleSetting.DefaultPositions();
        foreach (var (key, value) in RoleSetting.DefaultPositions())
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
    public void SaveRole(long roleId, RoleSetting setting) => Write(GetRolePath(roleId), setting);

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
