using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.World;
using Rpg.World.Views;

namespace Rpg.UI;

/// <summary>管理地面物品的 UI 信息、名称、输入与屏幕避让。</summary>
public partial class GroundItemOverlayControl : Node
{
    /// <summary>提供图标位置与世界命中的下层视图。</summary>
    private readonly GroundItemsView _worldView;
    /// <summary>当前 AOI 对象对应的信息控件。</summary>
    private readonly Dictionary<Guid, GroundItemInfoControl> _infoViews = new();
    /// <summary>UI 信息框画布。</summary>
    private CanvasLayer _infoLayer = null!;
    /// <summary>悬停名称画布。</summary>
    private ItemNamePopup _itemName = null!;
    /// <summary>最后观察的 ECS 对象状态。</summary>
    private DataWorldObjectsState? _previous;
    /// <summary>视口内最新的鼠标位置。</summary>
    private Vector2 _pointerPosition = new(-10000, -10000);
    /// <summary>失焦期间禁用物品输入。</summary>
    private bool _focused = true;
    /// <summary>当前世界会话的信息开关。</summary>
    private bool _infoVisible;
    /// <summary>信息框请求拾取的服务器对象身份。</summary>
    public event Action<Guid>? PickupRequested;

    /// <summary>绑定同一世界会话的世界表现。</summary>
    public GroundItemOverlayControl(GroundItemsView worldView) => _worldView = worldView;

    /// <summary>建立高于世界、低于 HUD 的信息框及指针浮层。</summary>
    public override void _Ready()
    {
        _infoLayer = new CanvasLayer { Name = "GroundItemInfoLayer", Layer = 5 };
        AddChild(_infoLayer);
        var pointerLayer = new CanvasLayer { Name = "GroundItemPointerLayer", Layer = 100 };
        AddChild(pointerLayer);
        _itemName = new ItemNamePopup { Name = "ItemNamePopup" };
        pointerLayer.AddChild(_itemName);
    }

    /// <summary>切换当前世界的信息框显隐。</summary>
    public void SetInfoVisible(bool visible)
    {
        _infoVisible = visible;
        if (!visible) foreach (var info in _infoViews.Values) info.Hide();
    }

    /// <summary>记录指针位置供摄像机移动时重新命中。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouse mouse) _pointerPosition = mouse.Position;
    }

    /// <summary>失焦时立即清除所有高亮。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            _focused = false;
            SetHovered(null, false);
        }
        if (what == NotificationApplicationFocusIn) _focused = true;
    }

    /// <summary>按 ECS 对象变化同步信息控件，并每帧更新屏幕布局与悬停。</summary>
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

    /// <summary>由 UI 遮挡规则过滤下层图标命中。</summary>
    public Guid? ItemAt(Vector2 position)
    {
        if (!_focused || GetViewport().GuiGetHoveredControl() is not null ||
            GetLastExclusiveWindow() != GetWindow()) return null;
        return _worldView.ItemAt(position);
    }

    /// <summary>按当前 ECS 对象集合创建或移除 UI 控件。</summary>
    private void Refresh(DataWorldObjectsState? snapshot)
    {
        foreach (var (id, info) in _infoViews.ToArray())
            if (snapshot is null || !snapshot.Objects.TryGetValue(id, out var value) || value.MonsterTypeId is not null)
            {
                info.QueueFree();
                _infoViews.Remove(id);
            }
        if (snapshot is null) return;
        foreach (var value in snapshot.Objects.Values.Where(value => value.MonsterTypeId is null))
        {
            if (!_infoViews.TryGetValue(value.ObjectId, out var info))
            {
                info = new GroundItemInfoControl(value.ObjectId);
                info.PickupRequested += id => PickupRequested?.Invoke(id);
                _infoViews.Add(value.ObjectId, info);
                _infoLayer.AddChild(info);
            }
            info.SetItem(GameServices.Config.Item.Items[value.ItemId].Name, value.Count);
            info.Visible = _infoVisible;
        }
    }

    /// <summary>按 GUID 顺序放置互不重叠的信息框。</summary>
    private void LayoutInfoViews()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var occupied = new List<Rect2>();
        foreach (var (id, info) in _infoViews.OrderBy(entry => entry.Key))
        {
            var anchor = _worldView.IconAnchor(id);
            var size = info.GetCombinedMinimumSize();
            if (anchor is not { } point || point.X < 0 || point.Y < 0 || point.X > viewport.X || point.Y > viewport.Y ||
                size.X > viewport.X || size.Y > viewport.Y)
            { info.Hide(); continue; }
            var placement = FindInfoPlacement(point, size, viewport, occupied);
            if (placement is not { } rect) { info.Hide(); continue; }
            info.Position = rect.Position;
            info.Size = size;
            info.Show();
            occupied.Add(rect);
        }
    }

    /// <summary>先垂直紧凑堆叠，再向两侧寻找视口内空位。</summary>
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

    /// <summary>获取当前鼠标所在的信息框身份。</summary>
    private Guid? InfoAt(Vector2 position)
    {
        if (!_infoVisible || !_focused) return null;
        var hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is not GroundItemInfoControl info || !info.Visible ||
            !_infoViews.ContainsKey(info.ObjectId) || !info.GetGlobalRect().HasPoint(position)) return null;
        return info.ObjectId;
    }

    /// <summary>同步 UI 提示与世界轮廓，保留信息框悬停优先级。</summary>
    private void SetHovered(Guid? id, bool overInfo)
    {
        _worldView.SetHovered(id);
        foreach (var (key, info) in _infoViews) info.SetHighlighted(key == id && overInfo);
        if (!overInfo && id is { } hovered && _previous is not null && _previous.Objects.TryGetValue(hovered, out var item))
            _itemName.ShowName(GameServices.Config.Item.Items[item.ItemId].Name, _pointerPosition);
        else _itemName.Hide();
    }
}
