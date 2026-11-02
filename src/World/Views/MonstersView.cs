using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>从 ECS 只读对象状态展示配置怪物，不承担怪物行为。</summary>
public partial class MonstersView : Node2D
{
    /// <summary>按服务器稳定身份复用贴图节点。</summary>
    private readonly Dictionary<Guid, Sprite2D> _views = new();
    /// <summary>最后同步的不可变对象集合。</summary>
    private DataWorldObjectsState? _previous;

    /// <summary>对象快照变化时更新位置、素材并移除离开 AOI 的怪物。</summary>
    public override void _Process(double delta)
    {
        var snapshot = GameServices.World.MapObjects;
        if (ReferenceEquals(snapshot, _previous)) return;
        _previous = snapshot;
        foreach (var (id, view) in _views.ToArray())
            if (snapshot is null || !snapshot.Objects.TryGetValue(id, out var value) ||
                !int.TryParse(value.MonsterTypeId, NumberStyles.None, CultureInfo.InvariantCulture, out var monsterId) ||
                !GameServices.Config.Monster.Items.ContainsKey(monsterId))
            {
                RemoveChild(view); view.QueueFree(); _views.Remove(id);
            }
        if (snapshot is null) return;
        foreach (var value in snapshot.Objects.Values)
        {
            if (!int.TryParse(value.MonsterTypeId, NumberStyles.None, CultureInfo.InvariantCulture, out var monsterId) ||
                !GameServices.Config.Monster.Items.TryGetValue(monsterId, out var config)) continue;
            if (!_views.TryGetValue(value.ObjectId, out var view))
            {
                view = new Sprite2D { Name = "Monster_" + value.ObjectId.ToString("N"), Centered = false,
                    TextureFilter = TextureFilterEnum.Nearest };
                _views.Add(value.ObjectId, view); AddChild(view);
            }
            view.Texture = ResourceLoader.Load<Texture2D>(config.TexturePath);
            view.Offset = -new Vector2(config.AnchorX, config.AnchorY);
            view.Position = IsometricProjection.ToScreen(new Vector2(value.X, value.Y));
        }
    }
}
