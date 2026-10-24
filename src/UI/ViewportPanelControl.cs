using Godot;

namespace Rpg.UI;

/// <summary>限制在视口内的居中弹窗，固定标题并滚动展示溢出内容。</summary>
public partial class ViewportPanelControl : PanelContainer
{
    /// <summary>显示在左侧标题栏的面板名称。</summary>
    public string Title { get; init; } = "";
    /// <summary>自然布局期望的最小宽度，小窗口时允许收窄。</summary>
    public float PreferredWidth { get; init; }
    /// <summary>业务控件加入的可滚动内容列。</summary>
    public VBoxContainer Content { get; private set; } = null!;
    /// <summary>标题与滚动内容的纵向布局。</summary>
    private VBoxContainer _column = null!;
    /// <summary>始终留在视口内的标题和关闭按钮。</summary>
    private HBoxContainer _header = null!;
    /// <summary>只滚动内容，避免标题和关闭按钮离开窗口。</summary>
    private ScrollContainer _scroll = null!;

    /// <summary>以左上原点创建弹窗，避免把锚点偏移写成负实际坐标。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _column = new VBoxContainer();
        AddChild(_column);
        _header = new HBoxContainer { Name = "PanelHeader" };
        _column.AddChild(_header);
        var title = new Label { Text = Title, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 20);
        _header.AddChild(title);
        var close = new Button { Name = "ClosePanel", Text = "关闭" };
        close.Pressed += Hide;
        _header.AddChild(close);
        _scroll = new ScrollContainer { Name = "PanelContentScroll", SizeFlagsVertical = SizeFlags.ExpandFill };
        _column.AddChild(_scroll);
        Content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(Content);
        VisibilityChanged += () => { if (Visible) FitToViewport(); };
        FitToViewport();
    }

    /// <summary>视口或异步容器尺寸变化时重新限制可见面板。</summary>
    public override void _Process(double delta)
    {
        if (Visible) FitToViewport();
    }

    /// <summary>按实际视口居中，内容过大时预留双向滚动条所需空间。</summary>
    public void FitToViewport()
    {
        if (_scroll is null) return;
        var viewport = GetViewportRect().Size;
        var available = (viewport - new Vector2(32, 32)).Max(Vector2.One);
        var header = _header.GetCombinedMinimumSize();
        var content = Content.GetCombinedMinimumSize() + _scroll.GetThemeStylebox("panel").GetMinimumSize();
        var padding = GetThemeStylebox("panel").GetMinimumSize();
        var desired = new Vector2(Mathf.Max(PreferredWidth, Mathf.Max(header.X, content.X) + padding.X),
            header.Y + _column.GetThemeConstant("separation") + content.Y + padding.Y);
        var barWidth = _scroll.GetVScrollBar().GetCombinedMinimumSize().X;
        var barHeight = _scroll.GetHScrollBar().GetCombinedMinimumSize().Y;
        var vertical = desired.Y > available.Y;
        var horizontal = desired.X > available.X;
        if (vertical && desired.X + barWidth > available.X) horizontal = true;
        if (horizontal && desired.Y + barHeight > available.Y) vertical = true;
        Size = (desired + new Vector2(vertical ? barWidth : 0, horizontal ? barHeight : 0)).Min(available);
        Position = ((viewport - Size) / 2).Max(Vector2.Zero);
    }
}
