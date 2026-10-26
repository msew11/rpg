using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.Utils;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>根据 ECS 只读状态绘制地面物品，不持有 UI 控件。</summary>
public partial class GroundItemsView : Node2D
{
    /// <summary>服务器对象身份到世界表现节点的映射。</summary>
    private readonly Dictionary<Guid, Node2D> _views = new();
    /// <summary>按素材键复用地面轮廓与图标。</summary>
    private readonly Dictionary<string, DataItemOutline> _resources = new();
    /// <summary>最后呈现的 ECS 对象读模型。</summary>
    private DataWorldObjectsState? _previous;
    /// <summary>GM 锚点标记的当前显隐。</summary>
    private bool _anchorsVisible;
    /// <summary>GM 锚点标记的当前颜色。</summary>
    private Color _anchorColor = Colors.Yellow;
    /// <summary>由 UI 选中的对象，供鼠标表现读取。</summary>
    public Guid? HoveredObjectId { get; private set; }

    /// <summary>更新当前及后续物品的锚点标记。</summary>
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

    /// <summary>供现有鼠标表现定位当前世界视图。</summary>
    public override void _Ready() => AddToGroup("GroundItemViews");

    /// <summary>只在 ECS 读模型变化时同步世界节点。</summary>
    public override void _Process(double delta)
    {
        var snapshot = GameServices.World.MapObjects;
        if (ReferenceEquals(snapshot, _previous)) return;
        _previous = snapshot;
        Refresh(snapshot);
    }

    /// <summary>复用仍在 AOI 中的图标节点，离开时移除。</summary>
    private void Refresh(DataWorldObjectsState? snapshot)
    {
        foreach (var (id, view) in _views.ToArray())
            if (snapshot is null || !snapshot.Objects.TryGetValue(id, out var value) || value.MonsterTypeId is not null)
            {
                RemoveChild(view);
                view.QueueFree();
                _views.Remove(id);
            }
        if (snapshot is null) { SetHovered(null); return; }
        var offsets = new Dictionary<(float X, float Y), int>();
        foreach (var value in snapshot.Objects.Values.Where(value => value.MonsterTypeId is null).OrderBy(value => value.ObjectId))
        {
            if (!_views.TryGetValue(value.ObjectId, out var view))
            {
                view = new Node2D { Name = "Item_" + value.ObjectId.ToString("N") };
                _views.Add(value.ObjectId, view);
                AddChild(view);
            }
            var point = (value.X, value.Y);
            var offset = offsets.GetValueOrDefault(point);
            offsets[point] = offset + 1;
            view.Position = IsometricProjection.ToScreen(new Vector2(value.X, value.Y));
            foreach (var child in view.GetChildren()) { view.RemoveChild(child); child.QueueFree(); }
            var visualOffset = new Vector2(0, offset * 48);
            var config = GameServices.Config.Item.Items[value.ItemId];
            if (!_resources.TryGetValue(config.GroundAssetId, out var resource))
                _resources[config.GroundAssetId] = resource = new DataItemOutline(GameServices.Items.Resolve(config.GroundAssetId));
            view.AddChild(new TextureRect { Name = "GroundItemOutline", Texture = resource.Outline,
                Position = new Vector2(-18, -26) + visualOffset, Size = new Vector2(36, 36), Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest });
            view.AddChild(new TextureRect { Name = "GroundItemIcon", Texture = resource.Icon,
                Position = new Vector2(-16, -24) + visualOffset, Size = new Vector2(32, 32),
                MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest });
            var marker = new EntityAnchorView { Name = "EntityAnchor", Visible = _anchorsVisible };
            marker.SetMarkerColor(_anchorColor);
            view.AddChild(marker);
        }
        if (HoveredObjectId is { } hovered && !_views.ContainsKey(hovered)) SetHovered(null);
        else SetHovered(HoveredObjectId);
    }

    /// <summary>返回图标顶部的视口锚点，供 UI 布置信息框。</summary>
    public Vector2? IconAnchor(Guid id)
    {
        if (!_views.TryGetValue(id, out var view)) return null;
        var icon = view.GetNode<TextureRect>("GroundItemIcon");
        return icon.GetGlobalTransformWithCanvas() * new Vector2(16, 0);
    }

    /// <summary>按图标的扩展矩形查询最上层物品，不处理 UI 遮挡。</summary>
    public Guid? ItemAt(Vector2 viewportPosition)
    {
        if (!IsVisibleInTree()) return null;
        foreach (var (id, view) in _views.Reverse())
        {
            var local = view.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
            var hitRect = new Rect2(view.GetNode<TextureRect>("GroundItemIcon").Position - new Vector2(8, 8), new Vector2(48, 48));
            if (hitRect.HasPoint(local)) return id;
        }
        return null;
    }

    /// <summary>只更新世界图标的轮廓与对外悬停身份。</summary>
    public void SetHovered(Guid? id)
    {
        HoveredObjectId = id;
        foreach (var (key, view) in _views)
            view.GetNode<TextureRect>("GroundItemOutline").Visible = key == id;
    }
}
