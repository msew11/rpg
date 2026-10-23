using System;
using Rpg.Services;
using Rpg.Settings.Data;

namespace Rpg.Settings;

/// <summary>拥有当前系统与角色设置，集中处理修改和保存。</summary>
public sealed class SettingService : IService
{
    /// <summary>仅负责本地读写的内部仓库。</summary>
    private readonly RepoSettings _repo;
    /// <summary>是否已释放服务。</summary>
    private bool _disposed;
    /// <summary>当前系统显示和声音设置。</summary>
    private readonly SystemSetting _systemSetting;
    /// <summary>当前选中角色的设置。</summary>
    private RoleSetting? _roleSetting;
    /// <summary>当前选中角色的标识。</summary>
    private long? _roleId;
    /// <summary>当前角色设置是否有未保存的修改。</summary>
    private bool _roleDirty;

    /// <summary>从指定目录载入本次启动的系统设置。</summary>
    public SettingService(string root)
    {
        _repo = new RepoSettings(root);
        _systemSetting = _repo.LoadSystem();
    }

    /// <summary>当前窗口宽度。</summary>
    public int Width { get { EnsureActive(); return _systemSetting.Width; } }
    /// <summary>当前窗口高度。</summary>
    public int Height { get { EnsureActive(); return _systemSetting.Height; } }
    /// <summary>当前主音量。</summary>
    public float MasterVolume { get { EnsureActive(); return _systemSetting.MasterVolume; } }
    /// <summary>切换当前角色并载入其独立设置。</summary>
    public void SelectRole(long roleId)
    {
        EnsureActive();
        SaveCurrentRole();
        _roleSetting = _repo.LoadRole(roleId);
        _roleId = roleId;
        _roleDirty = false;
    }

    /// <summary>修改并保存当前分辨率。</summary>
    public void SetResolution(int width, int height)
    {
        EnsureActive();
        _systemSetting.Width = width;
        _systemSetting.Height = height;
        _repo.SaveSystem(_systemSetting);
    }

    /// <summary>修改并保存当前主音量。</summary>
    public void SetMasterVolume(float volume)
    {
        EnsureActive();
        _systemSetting.MasterVolume = Math.Clamp(volume, 0, 1);
        _repo.SaveSystem(_systemSetting);
    }

    /// <summary>读取当前角色的技能栏显隐状态。</summary>
    public bool IsHotbarVisible(int index) => CurrentRole.VisibleHotbars[index - 1];

    /// <summary>修改并保存当前角色的技能栏显隐状态。</summary>
    public void SetHotbarVisible(int index, bool visible)
    {
        EnsureActive();
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
        EnsureActive();
        CurrentRole.HudPositions[key] = new HudPosition(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        _roleDirty = true;
    }

    /// <summary>恢复并保存当前角色的默认 HUD 布局。</summary>
    public void ResetHudPositions()
    {
        EnsureActive();
        CurrentRole.HudPositions = RoleSetting.DefaultPositions();
        _roleDirty = true;
        SaveCurrentRole();
    }

    /// <summary>保存当前角色的设置。</summary>
    public void SaveCurrentRole()
    {
        EnsureActive();
        if (!_roleDirty || _roleId is not long roleId) return;
        _repo.SaveRole(roleId, CurrentRole);
        _roleDirty = false;
    }

    /// <summary>需要先选中角色才能访问的当前角色设置。</summary>
    private RoleSetting CurrentRole
    {
        get
        {
            EnsureActive();
            return _roleSetting ?? throw new InvalidOperationException("尚未选择角色");
        }
    }


    /// <summary>系统设置文件的绝对路径。</summary>
    public string SystemPath { get { EnsureActive(); return _repo.SystemPath; } }
    /// <summary>当前角色的设置文件位置，未选角时不可访问。</summary>
    public string RolePath
    {
        get
        {
            EnsureActive();
            return _repo.GetRolePath(_roleId ?? throw new InvalidOperationException("尚未选择角色"));
        }
    }

    /// <summary>离开角色前保存布局，然后清空当前角色状态。</summary>
    public void LeaveRole()
    {
        EnsureActive();
        SaveCurrentRole();
        _roleSetting = null;
        _roleId = null;
    }

    /// <summary>服务退出时提交未保存布局，并幂等撤销角色状态。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        try { LeaveRole(); }
        finally { _disposed = true; _roleSetting = null; _roleId = null; }
    }

    /// <summary>防止已结束的设置服务继续操作。</summary>
    private void EnsureActive() => ObjectDisposedException.ThrowIf(_disposed, this);
}
