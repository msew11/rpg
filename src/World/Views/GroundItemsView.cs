using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Items;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>只读呈现唯一 AOI 缓存中的地面道具，不维护第二份业务状态或 ECS。</summary>
public partial class GroundItemsView : Node2D
{
    /// <summary>稳定 GUID 对应的表现节点，离开 AOI 后立即移除。</summary>
    private readonly Dictionary<Guid, Node2D> _views = new();
    /// <summary>资源图集解析缓存，不保存对象快照。</summary>
    private readonly ItemIconResolver _icons = new();
    /// <summary>上次观察的不可变快照引用，静止时跳过节点更新。</summary>
    private DataMapAoiSnapshotPbData? _previous;

    /// <summary>缓存引用变化时复用对象节点，离开世界时清除全部表现。</summary>
    public override void _Process(double delta)
    {
        var snapshot = GameServices.World.MapObjects;
        if (ReferenceEquals(snapshot, _previous)) return;
        _previous = snapshot;
        foreach (var (id, view) in _views.ToArray())
            if (snapshot is null || !snapshot.Objects.TryGetValue(id, out var value) || value.MonsterTypeId is not null)
            {
                RemoveChild(view);
                view.QueueFree();
                _views.Remove(id);
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
            // 同落点只错开表现，不改权威坐标，避免多次丢弃的图标和文字完全重叠。
            var point = (value.X, value.Y);
            var offset = offsets.GetValueOrDefault(point);
            offsets[point] = offset + 1;
            view.Position = IsometricProjection.ToScreen(new Vector2(value.X, value.Y)) + new Vector2(0, offset * 48);
            foreach (var child in view.GetChildren()) { view.RemoveChild(child); child.QueueFree(); }
            var config = GameServices.Config.Item.Items[value.ItemId];
            var texture = _icons.Resolve(config.IconAssetId);
            Control icon = texture is null
                ? new Label { Text = "?", HorizontalAlignment = HorizontalAlignment.Center }
                : new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest };
            icon.Name = "GroundItemIcon";
            icon.Position = new Vector2(-16, -24);
            icon.Size = new Vector2(32, 32);
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
            view.AddChild(icon);
            var label = new Label { Name = "GroundItemName", Text = $"{config.Name} ×{value.Count}",
                MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 4);
            view.AddChild(label);
            label.Position = new Vector2(-label.GetCombinedMinimumSize().X / 2, 10);
        }
    }
}
