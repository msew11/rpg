using Godot;

namespace Rpg.Settings.Data;

/// <summary>角色窗口的逻辑键及修饰键组合，数值可直接保存到 JSON。</summary>
public sealed record DataShortcut
{
    /// <summary>不含修饰标志的逻辑键。</summary>
    public Key Key { get; init; }
    /// <summary>是否同时按下 Ctrl。</summary>
    public bool Ctrl { get; init; }
    /// <summary>是否同时按下 Alt。</summary>
    public bool Alt { get; init; }
    /// <summary>是否同时按下 Shift。</summary>
    public bool Shift { get; init; }

    /// <summary>只允许常用字母、数字和功能键，保留系统关闭组合。</summary>
    public bool IsAllowed() => (Key is >= Key.A and <= Key.Z or >= Key.Key0 and <= Key.Key9 or >= Key.F1 and <= Key.F12)
        && !(Alt && Key == Key.F4);

    /// <summary>复制键盘事件的逻辑键与修饰状态。</summary>
    public static DataShortcut FromInput(InputEventKey input) => new()
    {
        Key = input.Keycode, Ctrl = input.CtrlPressed, Alt = input.AltPressed, Shift = input.ShiftPressed
    };

    /// <summary>提供游戏设置中显示的组合键名称。</summary>
    public string DisplayName() => (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + OS.GetKeycodeString(Key);
}
