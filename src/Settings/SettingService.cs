using Rpg.Config;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rpg.Services;
using Rpg.Settings.Data;

namespace Rpg.Settings;

/// <summary>拥有当前系统与角色设置，集中处理修改和保存。</summary>
public sealed class SettingService : Service
{
    /// <summary>构造绑定的文件提交日志。</summary>
    private readonly LogService _log;
    /// <summary>本次服务的设置持久化根目录。</summary>
    private readonly string _root;
    /// <summary>持久化 JSON 的格式设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    /// <summary>当前系统显示和声音设置。</summary>
    private readonly SystemSetting _systemSetting;
    /// <summary>当前选中角色的设置。</summary>
    private RoleSetting? _roleSetting;
    /// <summary>当前选中角色的标识。</summary>
    private long? _roleId;
    /// <summary>当前角色设置是否有未保存的修改。</summary>
    private bool _roleDirty;

    /// <summary>从环境指定的目录载入本次启动的系统设置。</summary>
    public SettingService(EnvService env, LogService log)
    {
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        ObjectDisposedException.ThrowIf(log.IsDisposed, log);
        _log = log;
        _root = env.SettingsDirectory;
        _systemSetting = LoadSystem();
    }

    /// <summary>当前窗口宽度。</summary>
    public int Width { get { ThrowIfDisposed(); return _systemSetting.Width; } }
    /// <summary>当前窗口高度。</summary>
    public int Height { get { ThrowIfDisposed(); return _systemSetting.Height; } }
    /// <summary>当前桌面窗口模式。</summary>
    public WindowDisplayMode WindowMode { get { ThrowIfDisposed(); return _systemSetting.WindowMode; } }
    /// <summary>当前主音量。</summary>
    public float MasterVolume { get { ThrowIfDisposed(); return _systemSetting.MasterVolume; } }
    /// <summary>切换当前角色并载入其独立设置。</summary>
    public void SelectRole(long roleId)
    {
        ThrowIfDisposed();
        SaveCurrentRole();
        _roleSetting = LoadRole(roleId);
        _roleId = roleId;
        _roleDirty = false;
    }

    /// <summary>修改并保存当前分辨率。</summary>
    public void SetResolution(int width, int height)
    {
        SetDisplay(width, height, WindowMode);
    }

    /// <summary>一次保存窗口尺寸与模式，分辨率始终代表普通窗口尺寸。</summary>
    public void SetDisplay(int width, int height, WindowDisplayMode mode)
    {
        ThrowIfDisposed();
        if (width < 800 || width > 3840) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 600 || height > 2160) throw new ArgumentOutOfRangeException(nameof(height));
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        _systemSetting.Width = width;
        _systemSetting.Height = height;
        _systemSetting.WindowMode = mode;
        Write(SystemPath, _systemSetting);
    }

    /// <summary>修改并保存当前主音量。</summary>
    public void SetMasterVolume(float volume)
    {
        ThrowIfDisposed();
        _systemSetting.MasterVolume = Math.Clamp(volume, 0, 1);
        Write(SystemPath, _systemSetting);
    }

    /// <summary>读取当前角色的技能栏显隐状态。</summary>
    public bool IsHotbarVisible(int index) => CurrentRole.VisibleHotbars[index - 1];

    /// <summary>修改并保存当前角色的技能栏显隐状态。</summary>
    public void SetHotbarVisible(int index, bool visible)
    {
        ThrowIfDisposed();
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

    /// <summary>读取普通窗口的保存位置副本，尚未拖动时返回空。</summary>
    public HudPosition? GetWindowPosition(string key) => CurrentRole.WindowPositions.TryGetValue(key, out var value)
        ? new HudPosition(value.X, value.Y) : null;

    /// <summary>在拖动结束时立即提交普通窗口位置，不影响 HUD 草稿。</summary>
    public void SaveWindowPosition(string key, float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        CurrentRole.WindowPositions[key] = new HudPosition(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        _roleDirty = true;
        SaveCurrentRole();
    }

    /// <summary>读取指定操作的不可变快捷键。</summary>
    public DataShortcut GetShortcut(string key) => CurrentRole.Shortcuts[key];

    /// <summary>校验范围与冲突，写入成功后才让新快捷键生效。</summary>
    public bool TrySetShortcut(string key, DataShortcut value, out string error)
    {
        var current = CurrentRole.Shortcuts;
        if (!current.ContainsKey(key)) throw new ArgumentException("未知快捷键项目", nameof(key));
        if (!value.IsAllowed()) { error = "该按键不可使用，请选择字母、数字或 F1–F12。"; return false; }
        if (current.Any(pair => pair.Key != key && pair.Value == value))
        { error = "该快捷键已被其他项目使用，请重新输入。"; return false; }
        var previous = current[key];
        current[key] = value;
        _roleDirty = true;
        try { SaveCurrentRole(); }
        catch { current[key] = previous; throw; }
        error = "";
        return true;
    }

    /// <summary>更新当前角色的 HUD 位置，提交时再保存。</summary>
    public void SetHudPosition(string key, float x, float y)
    {
        CurrentRole.HudPositions[key] = new HudPosition(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        _roleDirty = true;
    }

    /// <summary>读取当前角色聊天尺寸的副本，避免外部改动设置。</summary>
    public DataHudSize GetChatSize() => new() { Width = CurrentRole.ChatSize.Width, Height = CurrentRole.ChatSize.Height };

    /// <summary>更新聊天手动尺寸，由常态缩放结束或布局恢复确认时保存。</summary>
    public void SetChatSize(float width, float height)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height)) throw new ArgumentOutOfRangeException(nameof(width), "聊天尺寸必须为有限数值");
        CurrentRole.ChatSize = new DataHudSize
        {
            Width = Math.Max(DataHudSize.MinimumWidth, width), Height = Math.Max(DataHudSize.MinimumHeight, height)
        };
        _roleDirty = true;
    }

    /// <summary>恢复并保存当前角色的默认 HUD 位置与聊天尺寸。</summary>
    public void ResetHudPositions()
    {
        CurrentRole.HudPositions = RoleSetting.DefaultPositions();
        CurrentRole.ChatSize = new DataHudSize();
        _roleDirty = true;
        SaveCurrentRole();
    }

    /// <summary>保存当前角色的设置。</summary>
    public void SaveCurrentRole()
    {
        ThrowIfDisposed();
        if (!_roleDirty || _roleId is not long roleId) return;
        Write(GetRolePath(roleId), CurrentRole);
        _roleDirty = false;
    }

    /// <summary>需要先选中角色才能访问的当前角色设置。</summary>
    private RoleSetting CurrentRole
    {
        get
        {
            ThrowIfDisposed();
            return _roleSetting ?? throw new InvalidOperationException("尚未选择角色");
        }
    }


    /// <summary>系统设置文件的绝对路径。</summary>
    public string SystemPath { get { ThrowIfDisposed(); return Path.GetFullPath(Path.Combine(_root, "system.json")); } }
    /// <summary>当前角色的设置文件位置，未选角时不可访问。</summary>
    public string RolePath
    {
        get
        {
            ThrowIfDisposed();
            return GetRolePath(_roleId ?? throw new InvalidOperationException("尚未选择角色"));
        }
    }

    /// <summary>离开角色前保存布局，然后清空当前角色状态。</summary>
    public void LeaveRole()
    {
        ThrowIfDisposed();
        SaveCurrentRole();
        _roleSetting = null;
        _roleId = null;
    }

    /// <summary>服务退出时提交未保存布局，并幂等撤销角色状态。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        try { LeaveRole(); }
        finally { IsDisposed = true; _roleSetting = null; _roleId = null; }
    }

    /// <summary>指定角色设置文件的绝对路径。</summary>
    private string GetRolePath(long roleId) => Path.GetFullPath(Path.Combine(_root, "roles", $"{roleId}.json"));

    /// <summary>读取系统设置；文件缺失或损坏时采用默认值。</summary>
    private SystemSetting LoadSystem()
    {
        var setting = Read(SystemPath, new SystemSetting());
        if (setting.Width < 800 || setting.Width > 3840) setting.Width = 1280;
        if (setting.Height < 600 || setting.Height > 2160) setting.Height = 720;
        if (!Enum.IsDefined(setting.WindowMode)) setting.WindowMode = WindowDisplayMode.Windowed;
        if (!float.IsFinite(setting.MasterVolume)) setting.MasterVolume = 0.8f;
        setting.MasterVolume = Math.Clamp(setting.MasterVolume, 0, 1);
        return setting;
    }

    /// <summary>按角色 ID 读取独立设置。</summary>
    private RoleSetting LoadRole(long roleId)
    {
        var setting = Read(GetRolePath(roleId), new RoleSetting());
        setting.WindowPositions ??= new();
        foreach (var key in setting.WindowPositions.Keys.ToArray())
        {
            var position = setting.WindowPositions[key];
            if (position is null || !float.IsFinite(position.X) || !float.IsFinite(position.Y))
                setting.WindowPositions.Remove(key);
            else { position.X = Math.Clamp(position.X, 0, 1); position.Y = Math.Clamp(position.Y, 0, 1); }
        }
        setting.Shortcuts ??= RoleSetting.DefaultShortcuts();
        // 旧角色可能已将 Z 分配给其他操作，补入新动作时保留既有绑定。
        if (!setting.Shortcuts.ContainsKey("groundItems"))
        {
            var key = new[] { Godot.Key.Z, Godot.Key.X, Godot.Key.V, Godot.Key.G, Godot.Key.F }
                .First(candidate => setting.Shortcuts.Values.All(value => value != new DataShortcut { Key = candidate }));
            setting.Shortcuts["groundItems"] = new DataShortcut { Key = key };
        }
        foreach (var (key, value) in RoleSetting.DefaultShortcuts()) setting.Shortcuts.TryAdd(key, value);
        var shortcuts = RoleSetting.DefaultShortcuts().Keys.Select(key => setting.Shortcuts[key]).ToArray();
        if (shortcuts.Any(value => value is null || !value.IsAllowed()) || shortcuts.Distinct().Count() != shortcuts.Length)
            setting.Shortcuts = RoleSetting.DefaultShortcuts();
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
        if (setting.ChatSize is null || !float.IsFinite(setting.ChatSize.Width) || !float.IsFinite(setting.ChatSize.Height) ||
            setting.ChatSize.Width < DataHudSize.MinimumWidth || setting.ChatSize.Height < DataHudSize.MinimumHeight)
            setting.ChatSize = new DataHudSize();
        setting.VisibleHotbars[0] = true;
        return setting;
    }

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
    private void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        var content = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, true);
        _log.ClientIO(path, content);
    }
}
