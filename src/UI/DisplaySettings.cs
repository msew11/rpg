using Godot;
using Rpg.Settings.Data;

namespace Rpg.UI;

/// <summary>把本地显示设置应用到 Godot 顶层窗口。</summary>
public static class DisplaySettings
{
    /// <summary>切换模式，并在普通窗口模式恢复保存的尺寸和居中位置。</summary>
    public static void Apply(Window window, Vector2I windowSize, WindowDisplayMode mode)
    {
        if (mode == WindowDisplayMode.Windowed)
        {
            window.Mode = Window.ModeEnum.Windowed;
            window.Size = windowSize;
            window.MoveToCenter();
            return;
        }

        window.Mode = mode == WindowDisplayMode.Fullscreen
            ? Window.ModeEnum.ExclusiveFullscreen
            : Window.ModeEnum.Fullscreen;
    }
}
