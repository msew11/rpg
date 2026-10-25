using System;
using Godot;
using Rpg.Services;
using Rpg.Settings.Data;

namespace Rpg.UI;

/// <summary>常态聊天右上角缩放，固定左下角并在结束时保存位置与尺寸。</summary>
public partial class ChatResizeHandle : Control
{
    /// <summary>三角把手的逻辑像素边长。</summary>
    private const float CornerSize = 18;
    /// <summary>被调整的聊天面板。</summary>
    public Control Panel { get; init; } = null!;
    /// <summary>是否保持抓取，供布局与指针查询。</summary>
    public bool Resizing { get; private set; }
    /// <summary>实际命中三角时提示开手。</summary>
    public bool Hovering => IsVisibleInTree() && GetViewport().GuiGetHoveredControl() == this;
    /// <summary>缩放期间固定的左下角。</summary>
    private Vector2 _anchor;
    /// <summary>按下点到右上角的距离。</summary>
    private Vector2 _offset;

    /// <summary>登记指针查询并绑定真实 GUI 抓取。</summary>
    public override void _Ready()
    {
        Name = "ChatResizeHandle";
        TopLevel = true;
        Size = Vector2.One * CornerSize;
        MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("ChatResizeHandles");
        GuiInput += input =>
        {
            if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button) return;
            _anchor = new Vector2(Panel.GlobalPosition.X, Panel.GetGlobalRect().End.Y);
            _offset = GetGlobalTransform() * button.Position - (Panel.GlobalPosition + new Vector2(Panel.Size.X, 0));
            Resizing = true;
            AcceptEvent();
        };
        VisibilityChanged += () => { if (!IsVisibleInTree()) FinishResize(); };
        _Process(0);
    }

    /// <summary>把手贴住聊天右上角，不参与内容排版。</summary>
    public override void _Process(double delta) => GlobalPosition = Panel.GlobalPosition + new Vector2(Panel.Size.X - CornerSize, 0);

    /// <summary>仅三角内部接收输入，附近聊天内容仍可使用。</summary>
    public override bool _HasPoint(Vector2 point) => new Rect2(Vector2.Zero, Size).HasPoint(point) && Size.X - point.X + point.Y <= CornerSize;

    /// <summary>绘制常态缩放入口。</summary>
    public override void _Draw() => DrawColoredPolygon([new Vector2(Size.X - 2, 2), new Vector2(2, 2), new Vector2(Size.X - 2, Size.Y - 2)], new Color(0.45f, 0.75f, 0.8f));

    /// <summary>拖离把手继续缩放，松开提交；消费输入以隔离世界操作。</summary>
    public override void _Input(InputEvent input)
    {
        if (!Resizing) return;
        if (input is InputEventMouseMotion motion)
        {
            var minimum = Panel.GetCombinedMinimumSize().Max(new Vector2(DataHudSize.MinimumWidth, DataHudSize.MinimumHeight));
            var viewport = GetViewport().GetVisibleRect().Size;
            var desired = motion.Position - _offset;
            var right = Mathf.Clamp(desired.X, _anchor.X + minimum.X, Mathf.Max(_anchor.X + minimum.X, viewport.X));
            var top = Mathf.Clamp(desired.Y, 0, Mathf.Max(0, _anchor.Y - minimum.Y));
            Panel.Size = new Vector2(right - _anchor.X, _anchor.Y - top);
            Panel.GlobalPosition = new Vector2(_anchor.X, top);
            _Process(0);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            FinishResize();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>失焦时按窗口拖动规则保存并结束抓取。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) FinishResize();
    }

    /// <summary>节点离树时提交尚未松开的抓取，避免关闭界面丢失调整。</summary>
    public override void _ExitTree() => FinishResize();

    /// <summary>离开界面前补交正在缩放的位置和尺寸。</summary>
    public void FinishResize()
    {
        if (!Resizing) return;
        Resizing = false;
        if (!GameServices.IsInitialized || !GameServices.Session.HasRole) return;
        var position = Panel.Position / GetViewport().GetVisibleRect().Size.Max(Vector2.One);
        try
        {
            GameServices.Setting.SetChatSize(Panel.Size.X, Panel.Size.Y);
            GameServices.Setting.SetHudPosition("chat", position.X, position.Y);
            GameServices.Setting.SaveCurrentRole();
        }
        catch (Exception error) { GD.PushWarning($"无法保存聊天尺寸：{error.Message}"); }
    }
}
