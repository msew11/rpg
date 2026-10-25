using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Items;
using Rpg.UI;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>只读呈现唯一 AOI 缓存中的地面道具，不维护第二份业务状态或 ECS。</summary>
public partial class GroundItemsView : Node2D
{
    /// <summary>稳定 GUID 对应的表现节点，离开 AOI 后立即移除。</summary>
    private readonly Dictionary<Guid, Node2D> _views = new();
    /// <summary>当前 AOI 物品在 UI 画布上的信息控件。</summary>
    private readonly Dictionary<Guid, GroundItemInfoControl> _infoViews = new();
    /// <summary>资源图集解析缓存，不保存对象快照。</summary>
    private readonly ItemIconResolver _icons = new();
    /// <summary>图标资源按素材键复用，不保存第二份地图状态。</summary>
    private readonly Dictionary<string, DataItemOutline> _resources = new();
    /// <summary>输入事件提供的视口坐标，摄像机移动后也重新投影。</summary>
    private Vector2 _pointerPosition = new(-10000, -10000);
    /// <summary>失焦期间禁止悬停恢复。</summary>
    private bool _focused = true;
    /// <summary>与背包共用名称呈现，不保存地图业务数据。</summary>
    private ItemNamePopup _itemName = null!;
    /// <summary>信息框所在的独立 UI 画布层。</summary>
    private CanvasLayer _infoLayer = null!;
    /// <summary>当前会话的地面信息显隐状态。</summary>
    private bool _infoVisible;
    /// <summary>GM 实体原点标记的当前显隐状态。</summary>
    private bool _anchorsVisible;
    /// <summary>GM 实体原点标记的当前颜色。</summary>
    private Color _anchorColor = Colors.Yellow;
    /// <summary>当前悬停的稳定身份，供鼠标表现读取。</summary>
    public Guid? HoveredObjectId { get; private set; }
    /// <summary>信息框点击后请求拾取对应的对象。</summary>
    public event Action<Guid>? PickupRequested;

    /// <summary>切换当前 AOI 物品信息框的显示。</summary>
    public void SetInfoVisible(bool visible)
    {
        _infoVisible = visible;
        if (!visible)
            foreach (var info in _infoViews.Values) info.Hide();
    }

    /// <summary>同步当前及后续 AOI 物品的锚点标记。</summary>
    public void SetAnchorMarkers(bool visible, Color color)
    {
        _anchorsVisible = visible;
        _anchorColor = color;
        foreach (var view in _views.Values)
        {
            var marker = view.GetNode<EntityAnchorView>("EntityAnchor");
            marker.Visible = visible;
            marker.SetMarkerColor(color);
        }
    }

    /// <summary>标记表现组，鼠标可查询悬停状态而无需新服务或反向调用。</summary>
    public override void _Ready()
    {
        AddToGroup("GroundItemViews");
        _infoLayer = new CanvasLayer { Name = "GroundItemInfoLayer", Layer = 5 };
        AddChild(_infoLayer);
        var layer = new CanvasLayer { Name = "GroundItemPointerLayer", Layer = 100 };
        AddChild(layer);
        _itemName = new ItemNamePopup { Name = "ItemNamePopup" };
        layer.AddChild(_itemName);
    }

    /// <summary>只观察坐标，不抢占 GUI 或普通地图输入。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouse mouse) _pointerPosition = mouse.Position;
    }

    /// <summary>失焦立即清除高亮，重新聚焦后按实际悬停重新计算。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) { _focused = false; SetHovered(null); }
        if (what == NotificationApplicationFocusIn) _focused = true;
    }

    /// <summary>上次观察的不可变快照引用，静止时跳过节点更新。</summary>
    private DataMapAoiSnapshotPbData? _previous;

    /// <summary>缓存引用变化时复用对象节点，离开世界时清除全部表现。</summary>
    public override void _Process(double delta)
    {
        var snapshot = GameServices.World.MapObjects;
        if (!ReferenceEquals(snapshot, _previous))
        {
            _previous = snapshot;
            Refresh(snapshot);
        }
        if (_infoVisible) LayoutInfoViews();
        var infoId = InfoAt(_pointerPosition);
        SetHovered(infoId ?? ItemAt(_pointerPosition), infoId is not null);
    }

    /// <summary>只有快照变化才同步表现节点，静止悬停不重建资源。</summary>
    private void Refresh(DataMapAoiSnapshotPbData? snapshot)
    {
        foreach (var (id, view) in _views.ToArray())
            if (snapshot is null || !snapshot.Objects.TryGetValue(id, out var value) || value.MonsterTypeId is not null)
            {
                RemoveChild(view);
                view.QueueFree();
                _views.Remove(id);
                if (_infoViews.Remove(id, out var info)) info.QueueFree();
            }
        if (snapshot is null) return;
        var offsets = new Dictionary<(float X, float Y), int>();
        foreach (var value in snapshot.Objects.Values.Where(value => value.MonsterTypeId is null).OrderBy(value => value.ObjectId))
        {
            if (!_views.TryGetValue(value.ObjectId, out var view))
            {
                view = new Node2D { Name = "Item_" + value.ObjectId.ToString("N") };
                _views.Add(value.ObjectId, view);
                AddChild(view);
            }
            // 同落点只错开图标，不改权威坐标与信息框的 UI 层级。
            var point = (value.X, value.Y);
            var offset = offsets.GetValueOrDefault(point);
            offsets[point] = offset + 1;
            view.Position = IsometricProjection.ToScreen(new Vector2(value.X, value.Y));
            foreach (var child in view.GetChildren()) { view.RemoveChild(child); child.QueueFree(); }
            var visualOffset = new Vector2(0, offset * 48);
            var config = GameServices.Config.Item.Items[value.ItemId];
            if (!_resources.TryGetValue(config.GroundAssetId, out var resource))
                _resources[config.GroundAssetId] = resource = new DataItemOutline(_icons.Resolve(config.GroundAssetId));
            var outline = new TextureRect { Name = "GroundItemOutline", Texture = resource.Outline,
                Position = new Vector2(-18, -26) + visualOffset, Size = new Vector2(36, 36), Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest };
            view.AddChild(outline);
            var icon = new TextureRect { Name = "GroundItemIcon", Texture = resource.Icon,
                Position = new Vector2(-16, -24) + visualOffset, Size = new Vector2(32, 32),
                MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest };
            view.AddChild(icon);
            var marker = new EntityAnchorView { Name = "EntityAnchor", Visible = _anchorsVisible };
            marker.SetMarkerColor(_anchorColor);
            view.AddChild(marker);
            if (!_infoViews.TryGetValue(value.ObjectId, out var info))
            {
                info = new GroundItemInfoControl(value.ObjectId);
                info.PickupRequested += id => PickupRequested?.Invoke(id);
                _infoViews.Add(value.ObjectId, info);
                _infoLayer.AddChild(info);
            }
            info.SetItem(config.Name, value.Count);
            info.Visible = _infoVisible;
        }
    }

    /// <summary>按稳定对象顺序在视口内寻找不与已放置信息框相交的位置。</summary>
    private void LayoutInfoViews()
    {
        var viewport = GetViewportRect().Size;
        var occupied = new List<Rect2>();
        foreach (var (id, info) in _infoViews.OrderBy(entry => entry.Key))
        {
            var icon = _views[id].GetNode<TextureRect>("GroundItemIcon");
            var anchor = icon.GetGlobalTransformWithCanvas() * new Vector2(16, 0);
            var size = info.GetCombinedMinimumSize();
            if (anchor.X < 0 || anchor.Y < 0 || anchor.X > viewport.X || anchor.Y > viewport.Y ||
                size.X > viewport.X || size.Y > viewport.Y)
            { info.Hide(); continue; }
            var placement = FindInfoPlacement(anchor, size, viewport, occupied);
            if (placement is not { } rect) { info.Hide(); continue; }
            info.Position = rect.Position;
            info.Size = size;
            info.Show();
            occupied.Add(rect);
        }
    }

    /// <summary>优先在物品中心线上紧凑叠放，再向两侧寻找视口内空位。</summary>
    internal static Rect2? FindInfoPlacement(Vector2 anchor, Vector2 size, Vector2 viewport, IReadOnlyList<Rect2> occupied)
    {
        if (size.X > viewport.X || size.Y > viewport.Y) return null;
        for (var column = 0; column < 9; column++)
        for (var row = 0; row < 6; row++)
        {
            var side = column == 0 ? 0 : (column % 2 == 1 ? 1 : -1) * ((column + 1) / 2);
            var x = Mathf.Clamp(anchor.X - size.X / 2 + side * (size.X + 2), 0, viewport.X - size.X);
            var y = anchor.Y - size.Y - 4 - row * (size.Y + 2);
            if (y < 0) y = anchor.Y + 4 + row * (size.Y + 2);
            if (y < 0 || y + size.Y > viewport.Y) continue;
            var rect = new Rect2(new Vector2(x, y), size);
            if (occupied.Any(other => other.Grow(1).Intersects(rect))) continue;
            return rect;
        }
        return null;
    }

    /// <summary>信息框占用鼠标时仍能找出对应的地面物品。</summary>
    private Guid? InfoAt(Vector2 viewportPosition)
    {
        if (!_infoVisible || !_focused) return null;
        var hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is not GroundItemInfoControl info || !info.Visible ||
            !_infoViews.ContainsKey(info.ObjectId) || !info.GetGlobalRect().HasPoint(viewportPosition)) return null;
        return info.ObjectId;
    }

    /// <summary>返回最上层可见物品，UI 或模态窗口遮挡时不参与拾取。</summary>
    public Guid? ItemAt(Vector2 viewportPosition)
    {
        if (!_focused || !IsVisibleInTree() || GetViewport().GuiGetHoveredControl() is not null || GetLastExclusiveWindow() != GetWindow())
            return null;
        foreach (var (id, view) in _views.Reverse())
        {
            var local = view.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
            // 完整显示框外扩八像素，狭长图案和透明区域也容易选中；描边仍遵循 alpha。
            var hitRect = new Rect2(view.GetNode<TextureRect>("GroundItemIcon").Position - new Vector2(8, 8), new Vector2(48, 48));
            if (hitRect.HasPoint(local))
                return id;
        }
        return null;
    }

    /// <summary>一次只显示当前对象的真实轮廓，移出及失效时清除。</summary>
    private void SetHovered(Guid? id, bool overInfo = false)
    {
        HoveredObjectId = id;
        foreach (var (key, view) in _views) view.GetNode<TextureRect>("GroundItemOutline").Visible = key == id;
        foreach (var (key, info) in _infoViews) info.SetHighlighted(key == id && overInfo);
        if (_itemName is null) return;
        if (!overInfo && id is { } hovered && _previous is not null && _previous.Objects.TryGetValue(hovered, out var item))
            _itemName.ShowName(GameServices.Config.Item.Items[item.ItemId].Name, _pointerPosition);
        else _itemName.Hide();
    }

}
