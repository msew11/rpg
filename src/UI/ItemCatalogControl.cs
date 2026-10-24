using System;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rpg.UI;

/// <summary>按来源分类、搜索和分页浏览项目内道具及武器素材。</summary>
public partial class ItemCatalogControl : PanelContainer
{
    /// <summary>限制每次创建的素材按钮数量。</summary>
    internal const int PageSize = 48;
    /// <summary>首次打开后持有的静态目录快照。</summary>
    private DataItemAsset[] _entries = [];
    /// <summary>当前分类与搜索共同筛选后的条目。</summary>
    private DataItemAsset[] _filtered = [];
    /// <summary>分类选项与其总数量。</summary>
    private OptionButton _categories = null!;
    /// <summary>编号和来源搜索输入。</summary>
    private LineEdit _search = null!;
    /// <summary>仅放置当前页缩略图的网格。</summary>
    private GridContainer _grid = null!;
    /// <summary>网格区域的独立滚动容器。</summary>
    private ScrollContainer _scroll = null!;
    /// <summary>当前结果数量和页码。</summary>
    private Label _status = null!;
    /// <summary>当前选中条目的原像素区域预览。</summary>
    private TextureRect _preview = null!;
    /// <summary>当前条目的来源和尺寸信息。</summary>
    private Label _details = null!;
    /// <summary>没有匹配结果时的说明。</summary>
    private Label _empty = null!;
    /// <summary>回到上一页的按钮。</summary>
    private Button _previous = null!;
    /// <summary>进入下一页的按钮。</summary>
    private Button _next = null!;
    /// <summary>分类选项对应的原始名称，首项表示全部。</summary>
    private string[] _categoryNames = [];
    /// <summary>从零开始的当前页码。</summary>
    private int _page;

    /// <summary>创建界面；目录和纹理延迟到实际打开时加载。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("17212b"), BorderColor = new Color("405365"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8
        });
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 16);
        AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        var title = new Label { Text = "道具图鉴", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 24);
        header.AddChild(title);
        var close = new Button { Name = "CloseCatalog", Text = "关闭" };
        close.Pressed += Hide;
        header.AddChild(close);
        box.AddChild(new Label
        {
            Text = "按素材来源分类 · 暂无正式道具名称与装备类型 · 武器部件为首帧预览，含动作与方向变体",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var filters = new HBoxContainer();
        box.AddChild(filters);
        _categories = new OptionButton { Name = "CatalogCategory", CustomMinimumSize = new Vector2(240, 36) };
        _categories.ItemSelected += _ => Filter();
        filters.AddChild(_categories);
        _search = new LineEdit
        {
            Name = "CatalogSearch", PlaceholderText = "搜索素材编号或来源路径…", ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _search.TextChanged += _ => Filter();
        filters.AddChild(_search);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 16);
        box.AddChild(content);
        var results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(results);
        _empty = new Label { Text = "无匹配素材，请调整分类或搜索内容。", Visible = false };
        results.AddChild(_empty);
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        results.AddChild(_scroll);
        _grid = new GridContainer { Name = "CatalogGrid", Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(_grid);
        var detailScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(240, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        content.AddChild(detailScroll);
        var details = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailScroll.AddChild(details);
        details.AddChild(new Label { Text = "素材预览" });
        _preview = new TextureRect
        {
            Name = "CatalogPreview", CustomMinimumSize = new Vector2(220, 220),
            TextureFilter = TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        details.AddChild(_preview);
        _details = new Label { Name = "CatalogDetails", Text = "点击图标查看素材", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        details.AddChild(_details);
        var footer = new HBoxContainer();
        box.AddChild(footer);
        _status = new Label { Name = "CatalogStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddChild(_status);
        _previous = new Button { Name = "CatalogPrevious", Text = "上一页" };
        _previous.Pressed += () => { _page--; RenderPage(); };
        footer.AddChild(_previous);
        _next = new Button { Name = "CatalogNext", Text = "下一页" };
        _next.Pressed += () => { _page++; RenderPage(); };
        footer.AddChild(_next);
        UpdateLayout();
    }

    /// <summary>仅在打开期间随窗口大小调整可用区域。</summary>
    public override void _Process(double delta)
    {
        if (Visible) UpdateLayout();
    }

    /// <summary>首次读取项目内目录，构造分类计数并打开图鉴。</summary>
    public void Open()
    {
        if (_entries.Length == 0)
        {
            _entries = JsonSerializer.Deserialize<DataItemAsset[]>(FileAccess.GetFileAsString("res://assets/items/catalog.json"))
                ?? throw new InvalidOperationException("道具素材目录为空");
            _categoryNames = new[] { "" }.Concat(_entries.Select(entry => entry.Category).Distinct()).ToArray();
            foreach (var category in _categoryNames)
            {
                var count = category == "" ? _entries.Length : _entries.Count(entry => entry.Category == category);
                _categories.AddItem($"{(category == "" ? "全部素材" : category)}（{count:N0}）");
            }
            Filter();
        }
        Show();
        UpdateLayout();
    }

    /// <summary>按来源类别和编号搜索筛选，并重置页码和详情。</summary>
    private void Filter()
    {
        var category = _categoryNames[_categories.Selected];
        var query = _search.Text.Trim();
        _filtered = _entries.Where(entry => (category == "" || entry.Category == category)
            && entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _page = 0;
        _preview.Texture = null;
        _details.Text = "点击图标查看素材";
        RenderPage();
    }

    /// <summary>释放旧页节点，只为当前页加载所需图集区域。</summary>
    private void RenderPage()
    {
        var pages = Math.Max(1, (_filtered.Length + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var entry in _filtered.Skip(_page * PageSize).Take(PageSize))
        {
            var button = new Button { Name = "Asset" + (_page * PageSize + _grid.GetChildCount()),
                CustomMinimumSize = new Vector2(96, 102), TooltipText = entry.Source };
            var tile = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            tile.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            button.AddChild(tile);
            tile.AddChild(new TextureRect
            {
                Texture = Texture(entry), CustomMinimumSize = new Vector2(64, 70),
                TextureFilter = TextureFilterEnum.Nearest,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
            });
            var label = new Label { Text = entry.Name, HorizontalAlignment = HorizontalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 12);
            tile.AddChild(label);
            button.Pressed += () => ShowEntry(entry);
            _grid.AddChild(button);
        }
        _scroll.ScrollVertical = 0;
        _empty.Visible = _filtered.Length == 0;
        _status.Text = $"共 {_filtered.Length:N0} 条素材  ·  第 {_page + 1} / {pages} 页";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page == pages - 1;
    }

    /// <summary>显示选中素材的无损裁剪区域及溯源信息。</summary>
    private void ShowEntry(DataItemAsset entry)
    {
        _preview.Texture = Texture(entry);
        _details.Text = $"{entry.Name}\n\n{entry.Category}\n原图尺寸：{entry.Width} × {entry.Height}\n\n来源：{entry.Source}\n\n预览已裁除透明边缘，保持原始比例。";
    }

    /// <summary>按需加载图集并引用条目的原始像素区域。</summary>
    private static AtlasTexture Texture(DataItemAsset entry) => new()
    {
        Atlas = GD.Load<Texture2D>(entry.Atlas), Region = new Rect2(entry.X, entry.Y, entry.W, entry.H), FilterClip = true
    };

    /// <summary>保持图鉴在视口内，按可用宽度调整网格列数。</summary>
    private void UpdateLayout()
    {
        var viewport = GetViewportRect().Size;
        var desired = new Vector2(Math.Min(1180, viewport.X - 32), Math.Min(760, viewport.Y - 32));
        var columns = Math.Max(1, (int)((desired.X - 310) / 104));
        if (_grid.Columns != columns) _grid.Columns = columns;
        // 内容变化可能使容器临时撑高；布局稳定后恢复视口约束。
        if (Size != desired) Size = desired;
        var position = (viewport - Size) / 2;
        if (Position != position) Position = position;
    }
}
