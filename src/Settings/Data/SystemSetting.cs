namespace Rpg.Settings.Data;

/// <summary>系统设置中保存的桌面窗口模式。</summary>
public enum WindowDisplayMode
{
    /// <summary>有边框的普通窗口。</summary>
    Windowed,
    /// <summary>独占显示器的全屏模式。</summary>
    Fullscreen,
    /// <summary>覆盖显示器的无边框窗口模式。</summary>
    BorderlessFullscreen
}

/// <summary>与账号角色无关的显示和声音设置。</summary>
public sealed class SystemSetting
{
    /// <summary>窗口宽度。</summary>
    public int Width { get; set; } = 1280;
    /// <summary>窗口高度。</summary>
    public int Height { get; set; } = 720;
    /// <summary>桌面窗口模式；旧设置缺失时使用普通窗口。</summary>
    public WindowDisplayMode WindowMode { get; set; } = WindowDisplayMode.Windowed;
    /// <summary>主音量，范围为零到一。</summary>
    public float MasterVolume { get; set; } = 0.8f;
}
