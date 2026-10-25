using Godot;
using Rpg.Services;
using System;

namespace Rpg.UI;

/// <summary>通过标题栏拖动普通窗口，按角色保存位置并约束视口边界。</summary>
public partial class WindowDragControl : Node
{
    /// <summary>所属窗口在角色设置中的键。</summary>
    public string SettingKey { get; init; } = "";
    /// <summary>拥有位置与尺寸的弹窗。</summary>
    public Control Panel { get; init; } = null!;
    /// <summary>仅此标题区域允许开始拖动。</summary>
    public Control Header { get; init; } = null!;
    /// <summary>是否正由鼠标控制窗口位置。</summary>
    private bool _dragging;
    /// <summary>可见窗口是否正被抓取，供全局指针保持握手。</summary>
    public bool Dragging => _dragging && Panel.IsVisibleInTree();
    /// <summary>实际命中标题空白区时提示开手，关闭按钮保持自身交互。</summary>
    public bool Hovering => Panel.IsVisibleInTree() && GetViewport().GuiGetHoveredControl() == Header;
    /// <summary>按下位置与窗口原点的距离。</summary>
    private Vector2 _offset;
    /// <summary>当前界面载入或拖动后的相对坐标，排版期间不反复读取会话设置。</summary>
    private Vector2? _savedPosition;

    /// <summary>注册标题输入，关闭窗口时提交最后位置。</summary>
    public override void _Ready()
    {
        var saved = GameServices.Setting.GetWindowPosition(SettingKey);
        if (saved is not null) _savedPosition = new Vector2(saved.X, saved.Y);
        Header.MouseFilter = Control.MouseFilterEnum.Stop;
        AddToGroup("WindowDragControls");
        Header.MouseDefaultCursorShape = Control.CursorShape.Arrow;
        Header.GuiInput += OnHeaderInput;
        Panel.VisibilityChanged += () => { if (!Panel.IsVisibleInTree()) FinishDrag(); };
    }

    /// <summary>标题空白处按下后开始抓取，关闭按钮独立消费输入。</summary>
    private void OnHeaderInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
        _offset = Header.GetGlobalTransform() * mouse.Position - Panel.GlobalPosition;
        _dragging = true;
        Header.AcceptEvent();
    }

    /// <summary>指针离开标题仍持续拖动，松开即保存。</summary>
    public override void _Input(InputEvent input)
    {
        if (!_dragging) return;
        if (input is InputEventMouseMotion motion)
        {
            Panel.GlobalPosition = motion.Position - _offset;
            ClampPosition();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            FinishDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>窗口失焦也完成本次抓取，避免恢复后继续拖动。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) FinishDrag();
    }

    /// <summary>排版后恢复保存位置；未拖动窗口继续使用原默认位置。</summary>
    public void ApplyPosition(Vector2 defaultPosition)
    {
        if (!_dragging)
        {
            Panel.Position = _savedPosition is { } saved ? GetViewport().GetVisibleRect().Size * saved : defaultPosition;
        }
        ClampPosition();
    }

    /// <summary>完整窗口限制在当前视口内。</summary>
    private void ClampPosition() => Panel.Position = Panel.Position.Clamp(Vector2.Zero,
        (GetViewport().GetVisibleRect().Size - Panel.Size).Max(Vector2.Zero));

    /// <summary>完成拖动并写入角色设置，失败保留画面并报告。</summary>
    private void FinishDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        var viewport = GetViewport().GetVisibleRect().Size.Max(Vector2.One);
        _savedPosition = Panel.Position / viewport;
        if (!GameServices.IsInitialized || !GameServices.Session.HasRole) return;
        try { GameServices.Setting.SaveWindowPosition(SettingKey, _savedPosition.Value.X, _savedPosition.Value.Y); }
        catch (Exception error) { GD.PushWarning($"无法保存窗口位置：{error.Message}"); }
    }
}
