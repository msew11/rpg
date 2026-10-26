using Godot;

namespace Rpg.UI;

/// <summary>限制在视口内的弹窗，可选标题拖动，固定标题并滚动展示溢出内容。</summary>
public partial class ViewportPanelControl : PanelContainer
{
    /// <summary>避免高 DPI 缩放和容器取整将刚好放下的内容误判为溢出。</summary>
    private const float FitAllowance = 4f;
    /// <summary>显示在左侧标题栏的面板名称。</summary>
    public string Title { get; init; } = "";
    /// <summary>自然布局期望的最小宽度，小窗口时允许收窄。</summary>
    public float PreferredWidth { get; init; }
    /// <summary>可拖动窗口的角色设置键，空值表示保持居中。</summary>
    public string WindowSettingKey { get; init; } = "";
    /// <summary>普通窗口启用统一内边距；HUD 调整窗口保留原布局。</summary>
    public bool UseContentPadding { get; init; }
    /// <summary>选配的标题拖动与持久化行为。</summary>
    private WindowDragControl? _windowDrag;
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
        if (UseContentPadding) PopupPanelStyle.ApplyPadding(this);
        _column = new VBoxContainer();
        AddChild(_column);
        _header = new HBoxContainer { Name = "PanelHeader" };
        _column.AddChild(_header);
        _header.AddChild(PopupPanelStyle.CreateTitle(Title));
        var close = new Button { Name = "ClosePanel", Text = "关闭" };
        close.Pressed += Hide;
        _header.AddChild(close);
        _scroll = new ScrollContainer { Name = "PanelContentScroll", SizeFlagsVertical = SizeFlags.ExpandFill };
        _column.AddChild(_scroll);
        Content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(Content);
        if (WindowSettingKey.Length > 0)
        {
            _windowDrag = new WindowDragControl { Panel = this, Header = _header, SettingKey = WindowSettingKey };
            AddChild(_windowDrag);
        }
        VisibilityChanged += () => { if (Visible) FitToViewport(); };
        FitToViewport();
    }

    /// <summary>视口或异步容器尺寸变化时重新限制可见面板。</summary>
    public override void _Process(double delta)
    {
        if (Visible) FitToViewport();
    }

    /// <summary>按保存位置或默认居中布局，内容过大时预留双向滚动条空间。</summary>
    public void FitToViewport()
    {
        if (_scroll is null) return;
        var viewport = GetViewportRect().Size;
        var available = (viewport - new Vector2(32, 32)).Max(Vector2.One);
        var header = _header.GetCombinedMinimumSize();
        var content = Content.GetCombinedMinimumSize() + _scroll.GetThemeStylebox("panel").GetMinimumSize();
        var padding = GetThemeStylebox("panel").GetMinimumSize();
        var desired = new Vector2(Mathf.Max(PreferredWidth, Mathf.Max(header.X, content.X) + padding.X + FitAllowance),
            header.Y + _column.GetThemeConstant("separation") + content.Y + padding.Y + FitAllowance);
        var barWidth = _scroll.GetVScrollBar().GetCombinedMinimumSize().X;
        var barHeight = _scroll.GetHScrollBar().GetCombinedMinimumSize().Y;
        var vertical = desired.Y > available.Y;
        var horizontal = desired.X > available.X;
        if (vertical && desired.X + barWidth > available.X) horizontal = true;
        if (horizontal && desired.Y + barHeight > available.Y) vertical = true;
        Size = (desired + new Vector2(vertical ? barWidth : 0, horizontal ? barHeight : 0)).Min(available);
        var centered = ((viewport - Size) / 2).Max(Vector2.Zero);
        if (_windowDrag is null) Position = centered;
        else _windowDrag.ApplyPosition(centered);
    }
}
