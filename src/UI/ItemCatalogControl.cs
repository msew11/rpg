using Rpg.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rpg.UI;

/// <summary>按导出清单的两级分类浏览具名道具，并保留全部素材预览。</summary>
public partial class ItemCatalogControl : PanelContainer
{
    /// <summary>限制每次创建的素材按钮数量。</summary>
    internal const int PageSize = 48;
    /// <summary>首次打开后持有的静态目录快照。</summary>
    private DataItemAsset[] _entries = [];
    /// <summary>当前分类与搜索共同筛选后的条目。</summary>
    private DataItemAsset[] _filtered = [];
    /// <summary>与参考页面一致的父类型顺序。</summary>
    private static readonly string[] ParentCategories = ["武器", "防具／饰品", "其他装备", "宠物", "收集品", "消耗品"];
    /// <summary>全部具名道具记录，保留来源行和重复ID。</summary>
    private DataItemDefinition[] _definitions = [];
    /// <summary>当前父子类型及文字共同筛选的道具记录。</summary>
    private DataItemDefinition[] _filteredDefinitions = [];
    /// <summary>按素材唯一键查找精确关联的候选图标。</summary>
    private Dictionary<string, DataItemAsset> _assetById = new();
    /// <summary>在默认道具清单与原始素材预览间切换。</summary>
    private OptionButton _mode = null!;
    /// <summary>当前父类型内的子类型选项。</summary>
    private OptionButton _subtypes = null!;
    /// <summary>子类型索引对应的原始类型码，负一代表全部。</summary>
    private int[] _subtypeCodes = [-1];
    /// <summary>随模式显示分类确认程度或首帧预览说明。</summary>
    private Label _hint = null!;
    /// <summary>缺少候选图标时的明确占位。</summary>
    private Label _noPreview = null!;
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
    /// <summary>是否正在浏览图片素材而非道具记录。</summary>
    private bool IsAssetMode => _mode.Selected == 1;

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
        header.AddChild(PopupPanelStyle.CreateTitle("道具图鉴"));
        _mode = new OptionButton { Name = "CatalogMode" };
        _mode.AddItem("道具清单");
        _mode.AddItem("素材预览");
        _mode.ItemSelected += _ => RebuildCategories();
        header.AddChild(_mode);
        var close = new Button { Name = "CloseCatalog", Text = "关闭" };
        close.Pressed += Hide;
        header.AddChild(close);
        // 外层滚动只承载内容，避免窄窗口被筛选栏最小宽度撑出边界。
        var bodyScroll = new ScrollContainer { Name = "CatalogBodyScroll", SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(bodyScroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        bodyScroll.AddChild(body);
        _hint = new Label
        {
            Text = "分类来自道具导出清单 · 装备及子类型语义沿用来源推定 · 图标为编号匹配候选",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        body.AddChild(_hint);
        var filters = new HBoxContainer();
        body.AddChild(filters);
        _categories = new OptionButton { Name = "CatalogCategory", CustomMinimumSize = new Vector2(210, 36), FitToLongestItem = false };
        _categories.ItemSelected += _ => RebuildSubtypes();
        filters.AddChild(_categories);
        _subtypes = new OptionButton { Name = "CatalogSubtype", CustomMinimumSize = new Vector2(220, 36), FitToLongestItem = false };
        _subtypes.ItemSelected += _ => Filter();
        filters.AddChild(_subtypes);
        _search = new LineEdit
        {
            Name = "CatalogSearch", PlaceholderText = "搜索道具名称、ID、说明…", ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _search.TextChanged += _ => Filter();
        filters.AddChild(_search);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 16);
        body.AddChild(content);
        var results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(results);
        _empty = new Label { Text = "无匹配内容，请调整分类或搜索内容。", Visible = false };
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
        details.AddChild(new Label { Text = "图像与详情" });
        _preview = new TextureRect
        {
            Name = "CatalogPreview", CustomMinimumSize = new Vector2(220, 220),
            TextureFilter = TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        details.AddChild(_preview);
        _noPreview = new Label { Name = "CatalogMissingIcon", Text = "暂无图标候选", Visible = false };
        details.AddChild(_noPreview);
        _details = new Label { Name = "CatalogDetails", Text = "点击图标查看素材", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        details.AddChild(_details);
        var footer = new HBoxContainer();
        body.AddChild(footer);
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

    /// <summary>首次读取项目内图片与道具清单，默认按参考页面分类。</summary>
    public void Open()
    {
        if (_entries.Length == 0)
        {
            _entries = ItemAtlasUtil.LoadCatalog();
            if (_entries.Length == 0) throw new InvalidOperationException("道具素材目录为空");
            _assetById = ItemAtlasUtil.Index(_entries);
            _definitions = JsonSerializer.Deserialize<DataItemDefinition[]>(FileAccess.GetFileAsString("res://assets/items/definitions.json"))
                ?? throw new InvalidOperationException("具名道具清单为空");
            RebuildCategories();
        }
        Show();
        UpdateLayout();
    }

    /// <summary>切换模式时重建父类型数量，清除旧查询与子类型。</summary>
    private void RebuildCategories()
    {
        _categories.Clear();
        _categoryNames = new[] { "" }.Concat(IsAssetMode
            ? _entries.Select(entry => entry.Category).Distinct() : ParentCategories).ToArray();
        foreach (var category in _categoryNames)
        {
            var count = IsAssetMode
                ? _entries.Count(entry => category == "" || entry.Category == category)
                : _definitions.Count(entry => category == "" || entry.Category == category);
            _categories.AddItem($"{(category == "" ? "全部" : category)}（{count:N0}）");
        }
        _hint.Text = IsAssetMode
            ? "按图片来源分类 · 武器部件为首帧预览，含动作与方向变体"
            : "分类来自道具导出清单 · 装备及子类型语义沿用来源推定 · 图标为编号匹配候选";
        _search.PlaceholderText = IsAssetMode ? "搜索素材编号或来源路径…" : "搜索道具名称、ID、说明…";
        _search.Text = "";
        RebuildSubtypes();
    }

    /// <summary>只列出当前父类型内的子类型，按原码排序并回到全部子类型。</summary>
    private void RebuildSubtypes()
    {
        _subtypes.Clear();
        _subtypes.Visible = !IsAssetMode;
        var category = _categoryNames[_categories.Selected];
        var groups = _definitions.Where(entry => entry.Category == category)
            .GroupBy(entry => entry.TypeCode).OrderBy(group => group.Key).ToArray();
        _subtypeCodes = new[] { -1 }.Concat(groups.Select(group => group.Key)).ToArray();
        var count = category == "" ? _definitions.Length : groups.Sum(group => group.Count());
        _subtypes.AddItem(category == "" ? "全部子类型（先选父类型）" : $"全部子类型（{count:N0}）");
        foreach (var group in groups) _subtypes.AddItem($"{group.First().TypeName}（{group.Count():N0}）");
        _subtypes.Disabled = category == "";
        Filter();
    }

    /// <summary>组合父子类型与文字检索，变化后重置页码和详情。</summary>
    private void Filter()
    {
        var category = _categoryNames[_categories.Selected];
        var query = _search.Text.Trim();
        if (IsAssetMode)
            _filtered = _entries.Where(entry => (category == "" || entry.Category == category)
                && entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        else
        {
            var code = _subtypeCodes[_subtypes.Selected];
            _filteredDefinitions = _definitions.Where(entry => (category == "" || entry.Category == category)
                && (code == -1 || entry.TypeCode == code)
                && (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || entry.ItemId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    || entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        }
        _page = 0;
        _preview.Texture = null;
        _noPreview.Hide();
        _details.Text = "点击条目查看详情";
        RenderPage();
    }

    /// <summary>释放旧页节点，只为当前页加载所需图集区域。</summary>
    private void RenderPage()
    {
        var count = IsAssetMode ? _filtered.Length : _filteredDefinitions.Length;
        var pages = Math.Max(1, (count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        if (IsAssetMode)
        {
            foreach (var entry in _filtered.Skip(_page * PageSize).Take(PageSize))
                AddTile(entry, entry.Name, "", entry.Source, entry.Id, () => ShowEntry(entry));
        }
        else
            foreach (var item in _filteredDefinitions.Skip(_page * PageSize).Take(PageSize))
                AddTile(_assetById.GetValueOrDefault(item.IconAssetId), item.Name, $"ID {item.ItemId}",
                    $"{item.Name} · ID {item.ItemId}\n{item.Source} 第{item.SourceRow}行", item.Key, () => ShowItem(item));
        _scroll.ScrollVertical = 0;
        _empty.Visible = count == 0;
        _status.Text = $"共 {count:N0} 条{(IsAssetMode ? "素材" : "道具记录")}  ·  第 {_page + 1} / {pages} 页";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page == pages - 1;
    }

    /// <summary>创建带名称与唯一来源键的条目按钮，缺图记录使用文字占位。</summary>
    private void AddTile(DataItemAsset? asset, string name, string subtitle, string tooltip, string key, Action selected)
    {
        var button = new Button { Name = "Asset" + (_page * PageSize + _grid.GetChildCount()),
            CustomMinimumSize = new Vector2(96, subtitle == "" ? 102 : 120), TooltipText = tooltip };
        button.SetMeta("entry_key", key);
        var tile = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        tile.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.AddChild(tile);
        if (asset is not null)
            tile.AddChild(new TextureRect
            {
                Texture = Texture(asset), CustomMinimumSize = new Vector2(64, 70),
                TextureFilter = TextureFilterEnum.Nearest, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
            });
        else
            tile.AddChild(new Label { Text = "暂无图标", CustomMinimumSize = new Vector2(64, 70),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore });
        var label = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 12);
        tile.AddChild(label);
        if (subtitle != "")
        {
            var id = new Label { Text = subtitle, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            id.AddThemeFontSizeOverride("font_size", 11);
            tile.AddChild(id);
        }
        button.Pressed += selected;
        _grid.AddChild(button);
    }

    /// <summary>显示完整具名道具信息，保留分类推定状态及图标候选性质。</summary>
    private void ShowItem(DataItemDefinition item)
    {
        var asset = _assetById.GetValueOrDefault(item.IconAssetId);
        _preview.Texture = asset is null ? null : Texture(asset);
        _noPreview.Visible = asset is null;
        _details.Text = $"{item.Name}\nID {item.ItemId}\n\n{item.Category} / {item.TypeName}\n原始类型码：{item.TypeCode}\n背包归属：{item.InventoryType}\n\n"
            + $"{(item.Description == "" ? "暂无说明" : item.Description)}\n\n分类依据：{item.CategoryStatus}\n子类型状态：{item.TypeStatus}\n\n"
            + $"来源：{item.Source} 第{item.SourceRow}行\n说明匹配：{item.DescriptionMatch}（{item.DescriptionCandidates}个候选）\n\n"
            + (asset is null ? "暂无图标候选" : $"候选图标：{asset.Source}\n原图尺寸：{asset.Width} × {asset.Height}");
    }

    /// <summary>显示选中素材的无损裁剪区域及溯源信息。</summary>
    private void ShowEntry(DataItemAsset entry)
    {
        _preview.Texture = Texture(entry);
        _noPreview.Hide();
        _details.Text = $"{entry.Name}\n\n{entry.Category}\n原图尺寸：{entry.Width} × {entry.Height}\n\n来源：{entry.Source}\n\n预览已裁除透明边缘，保持原始比例。";
    }

    /// <summary>按需加载图集并引用条目的原始像素区域。</summary>
    private static AtlasTexture? Texture(DataItemAsset entry) => ItemAtlasUtil.Texture(entry);

    /// <summary>保持图鉴在视口内，按可用宽度调整网格列数。</summary>
    private void UpdateLayout()
    {
        var viewport = GetViewportRect().Size;
        var desired = new Vector2(Math.Min(1180, viewport.X - 32), Math.Min(760, viewport.Y - 32)).Max(Vector2.One);
        var columns = Math.Max(1, (int)((desired.X - 310) / 104));
        if (_grid.Columns != columns) _grid.Columns = columns;
        // 内容变化可能使容器临时撑高；布局稳定后恢复视口约束。
        if (Size != desired) Size = desired;
        var position = (viewport - Size) / 2;
        if (Position != position) Position = position;
    }
}
