using Godot;

namespace Rpg.UI;

/// <summary>不参与 HUD 容器排版的绿色调整覆盖框，整区接收拖动。</summary>
public partial class HudEditOverlay : Control
{
    /// <summary>框内左上角显示的 HUD 名称。</summary>
    public string Title { get; set; } = "";
    /// <summary>是否正在被按住拖动，供全局指针保持握手。</summary>
    public bool Dragging { get; set; }
    /// <summary>高亮边框与名称使用的绿色。</summary>
    private static readonly Color Highlight = new(0.25f, 1f, 0.4f);

    /// <summary>注册指针查询组，并在外框变化时刷新绘制。</summary>
    public override void _Ready()
    {
        AddToGroup("HudEditOverlays");
        MouseFilter = MouseFilterEnum.Stop;
        Resized += QueueRedraw;
    }

    /// <summary>跟随父 HUD 的实际外框，不增加内容最小尺寸。</summary>
    public override void _Process(double delta)
    {
        var panel = GetParent<Control>();
        GlobalPosition = panel.GlobalPosition;
        Size = panel.Size;
    }

    /// <summary>绘制绿色外框、淡色高亮与框内左上角名称标签。</summary>
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.15f, 0.9f, 0.3f, 0.1f));
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), Highlight, false, 2);
        const float labelLeft = 3;
        var font = ThemeDB.FallbackFont;
        var textSize = font.GetStringSize(Title, fontSize: 13);
        DrawRect(new Rect2(new Vector2(labelLeft, 3), textSize + new Vector2(8, 2)), new Color(0.02f, 0.12f, 0.04f, 0.95f));
        DrawString(font, new Vector2(labelLeft + 4, 4 + font.GetAscent(13)), Title, fontSize: 13, modulate: Highlight);
    }
}
