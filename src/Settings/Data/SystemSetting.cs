namespace Rpg.Settings.Data;

/// <summary>与账号角色无关的显示和声音设置。</summary>
public sealed class SystemSetting
{
    /// <summary>窗口宽度。</summary>
    public int Width { get; set; } = 1280;
    /// <summary>窗口高度。</summary>
    public int Height { get; set; } = 720;
    /// <summary>主音量，范围为零到一。</summary>
    public float MasterVolume { get; set; } = 0.8f;
}
