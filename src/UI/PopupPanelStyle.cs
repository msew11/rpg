using Godot;

namespace Rpg.UI;

/// <summary>为指定的普通窗口提供一致内边距，同时保留原有主题外观。</summary>
public static class PopupPanelStyle
{
    /// <summary>标题栏和内容距离窗口四边的最小逻辑像素。</summary>
    public const float ContentInset = 12f;

    /// <summary>复制当前面板样式后设置四边边距，避免影响共享主题及 HUD。</summary>
    public static void ApplyPadding(PanelContainer panel)
    {
        var style = (StyleBox)panel.GetThemeStylebox("panel").Duplicate();
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
            style.SetContentMargin(side, Mathf.Max(ContentInset, style.GetContentMargin(side)));
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
