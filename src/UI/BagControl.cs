using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Rpg.Services;

namespace Rpg.UI;

/// <summary>从角色快照绘制图标与数量的只读背包，滚动网格适配小窗口。</summary>
public partial class BagControl : PanelContainer
{
    /// <summary>按基础配置列数排列的格子网格。</summary>
    private GridContainer _grid = null!;
    /// <summary>首次显示时加载的素材索引，不依赖 GM 图鉴窗口。</summary>
    private Dictionary<string, DataItemAsset>? _assets;
    /// <summary>已创建的固定格子，刷新只替换内容。</summary>
    private readonly List<PanelContainer> _slots = [];

    /// <summary>创建标题、关闭按钮与双向滚动网格。</summary>
    public override void _Ready()
    {
        Name = "背包";
        MouseFilter = MouseFilterEnum.Stop;
        var column = new VBoxContainer();
        AddChild(column);
        var header = new HBoxContainer();
        column.AddChild(header);
        header.AddChild(new Label { Text = "背包", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "关闭" };
        close.Pressed += Hide;
        header.AddChild(close);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(scroll);
        _grid = new GridContainer { Name = "BagGrid" };
        scroll.AddChild(_grid);
        VisibilityChanged += () => { if (Visible) Refresh(); };
        _Process(0);
    }

    /// <summary>将面板保持在视口内，网格超出时由滚动区承载。</summary>
    public override void _Process(double delta)
    {
        var viewport = GetViewportRect().Size;
        Size = new Vector2(Mathf.Min(480, Mathf.Max(120, viewport.X - 32)), Mathf.Min(370, Mathf.Max(100, viewport.Y - 32)));
        Position = new Vector2(Mathf.Min(Mathf.Max(432, (viewport.X - Size.X) / 2), Mathf.Max(16, viewport.X - Size.X - 16)),
            (viewport.Y - Size.Y) / 2);
    }

    /// <summary>读取最新只读缓存，按格子呈现图标、全部数量和名称提示。</summary>
    public void Refresh()
    {
        if (_grid is null || !Visible) return;
        var config = GameServices.Config;
        var capacity = config.Basic.Value.GetBagCapacity(GameServices.Session.Role.BagExtraRows);
        _grid.Columns = config.Basic.Value.BagColumns;
        if (_slots.Count != capacity)
        {
            foreach (var slot in _slots) { _grid.RemoveChild(slot); slot.QueueFree(); }
            _slots.Clear();
            for (var index = 0; index < capacity; index++)
            {
                var slot = new PanelContainer { Name = $"Slot{index}", CustomMinimumSize = new Vector2(48, 48), MouseFilter = MouseFilterEnum.Stop };
                var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.13f, 0.17f), BorderColor = new Color(0.35f, 0.40f, 0.48f) };
                style.SetBorderWidthAll(1);
                slot.AddThemeStyleboxOverride("panel", style);
                _grid.AddChild(slot);
                _slots.Add(slot);
            }
        }
        foreach (var slot in _slots)
        {
            slot.TooltipText = "";
            foreach (var child in slot.GetChildren()) { slot.RemoveChild(child); child.QueueFree(); }
        }
        foreach (var item in GameServices.Session.Bag.Items)
        {
            var definition = config.Item.Items[item.ItemId];
            var slot = _slots[item.SlotIndex];
            slot.TooltipText = definition.Name;
            var content = new Control { CustomMinimumSize = new Vector2(46, 46), MouseFilter = MouseFilterEnum.Ignore };
            slot.AddChild(content);
            var texture = ResolveTexture(definition.IconAssetId);
            Control icon = texture is null
                ? new Label { Name = "MissingIcon", Text = "?", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new TextureRect { Name = "ItemIcon", Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest };
            content.AddChild(icon);
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            icon.MouseFilter = MouseFilterEnum.Ignore;
            var count = new Label { Name = "ItemCount", Text = item.Count.ToString(), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = MouseFilterEnum.Ignore };
            count.AddThemeColorOverride("font_outline_color", Colors.Black);
            count.AddThemeConstantOverride("outline_size", 4);
            content.AddChild(count);
            count.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    /// <summary>按显式素材键取图集区域，缺失时由格子显示占位。</summary>
    private AtlasTexture? ResolveTexture(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _assets ??= (JsonSerializer.Deserialize<DataItemAsset[]>(FileAccess.GetFileAsString("res://assets/items/catalog.json")) ?? [])
            .ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        if (!_assets.TryGetValue(id, out var asset) || !ResourceLoader.Exists(asset.Atlas)) return null;
        return new AtlasTexture { Atlas = GD.Load<Texture2D>(asset.Atlas), Region = new Rect2(asset.X, asset.Y, asset.W, asset.H), FilterClip = true };
    }
}
