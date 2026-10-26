using Godot;

namespace Rpg.UI;

/// <summary>共用角色页面布局：中央预览、右侧内容及右下角返回。</summary>
public abstract partial class RoleScreenControl : Control
{
    /// <summary>中央角色使用两倍展示，并预留脚部与下方旋转按钮的间距。</summary>
    protected RolePreviewControl Preview { get; } = new()
        { Name = "RolePreview", ActorScale = 2, ActorBottomMargin = 32 };
    /// <summary>右栏中的列表或创建选项。</summary>
    protected VBoxContainer RightContent { get; } = new() { Name = "RightContent" };
    /// <summary>页面统一的右下角返回按钮。</summary>
    protected Button BackButton { get; } = new() { CustomMinimumSize = new Vector2(0, 40) };
    /// <summary>可供主控制器更新的请求与失败反馈。</summary>
    public Label Feedback { get; } = new() { Name = "RoleFeedback", AutowrapMode = TextServer.AutowrapMode.WordSmart,
        HorizontalAlignment = HorizontalAlignment.Center };
    /// <summary>右侧面板外框。</summary>
    private readonly PanelContainer _rightPanel = new() { Name = "RightPanel" };

    /// <summary>建立全屏背景与布局节点，再由具体页面填入内容。</summary>
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("13242d"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        AddChild(Preview);
        AddChild(_rightPanel);
        var margin = new MarginContainer();
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 12);
        _rightPanel.AddChild(margin);
        margin.AddChild(RightContent);
        AddChild(BackButton);
        Preview.Actions.AddChild(Feedback);
        Resized += Arrange;
        Arrange();
    }

    /// <summary>小窗口让中央操作避开右栏，右下角返回保持在屏幕边距内。</summary>
    private void Arrange()
    {
        var width = Mathf.Clamp(Size.X * 0.28f, 280, 380);
        var right = Size.X - width - 24;
        _rightPanel.Position = new Vector2(right, 32);
        _rightPanel.Size = new Vector2(width, Mathf.Max(300, Size.Y - 120));
        BackButton.Position = new Vector2(right, Size.Y - 64);
        BackButton.Size = new Vector2(width, 40);
        var center = Mathf.Min(Size.X / 2, right - 180);
        Preview.Position = new Vector2(center - 160, 48);
        Preview.Size = new Vector2(320, Mathf.Max(360, Size.Y - 112));
    }
}
