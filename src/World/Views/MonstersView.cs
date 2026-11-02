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
    /// <summary>每种贴图只读取一次 CPU 像素，供可见区域命中。</summary>
    private readonly Dictionary<Texture2D, Image> _images = new();
    /// <summary>唯一的脚底选中动画，切换目标时复用。</summary>
    private AnimatedSprite2D _selection = null!;
    /// <summary>最近鼠标输入的视口位置，供相机变化后重新命中。</summary>
    private Vector2 _pointerPosition = new(-10000, -10000);
    /// <summary>失焦期间不显示怪物悬停。</summary>
    private bool _focused = true;
    /// <summary>放置模式及离开世界时禁止怪物输入。</summary>
    public bool InteractionEnabled { get; set; } = true;
    /// <summary>只在当前客户端世界内保留的唯一目标身份。</summary>
    public Guid? SelectedObjectId { get; private set; }
    /// <summary>当前未被界面遮挡的怪物身份。</summary>
    public Guid? HoveredObjectId => MonsterAt(_pointerPosition);
    /// <summary>只有当前世界可接收未遮挡输入时才允许选择和地面取消。</summary>
    public bool CanInteract => InteractionEnabled && _focused && IsVisibleInTree() &&
        GetViewport().GuiGetHoveredControl() is null && GetLastExclusiveWindow() == GetWindow();

    /// <summary>注册指针查询入口，并建立保留原始锚点的六帧循环动画。</summary>
    public override void _Ready()
    {
        AddToGroup("MonsterViews");
        var frames = new SpriteFrames();
        frames.SetAnimationSpeed("default", 10);
        frames.SetAnimationLoopMode("default", SpriteFrames.LoopMode.Linear);
        for (var index = 0; index < 6; index++)
            frames.AddFrame("default", GD.Load<Texture2D>($"res://assets/monsters/selection/ring_{index}.png"));
        _selection = new AnimatedSprite2D { Name = "MonsterSelectionRing", SpriteFrames = frames,
            Centered = false, Offset = new Vector2(-34, -20), ShowBehindParent = true,
            TextureFilter = TextureFilterEnum.Nearest, Visible = false };
        AddChild(_selection);
    }

    /// <summary>只观察指针，不在 GUI 处理前消费选中输入。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouse mouse) _pointerPosition = mouse.Position;
    }

    /// <summary>应用失焦时禁用悬停，重新激活后恢复实时查询。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) _focused = false;
        if (what == NotificationApplicationFocusIn) _focused = true;
    }

    /// <summary>按绘制顺序查询真实可见像素，正确逆变换相机缩放与平移。</summary>
    public Guid? MonsterAt(Vector2 viewportPosition)
    {
        if (!CanInteract) return null;
        foreach (var (id, view) in _views.OrderByDescending(pair => pair.Value.Position.Y).ThenByDescending(pair => pair.Value.GetIndex()))
        {
            if (!view.IsVisibleInTree() || view.Texture is not { } texture) continue;
            var point = view.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
            var rect = view.GetRect();
            if (!rect.HasPoint(point)) continue;
            if (!_images.TryGetValue(texture, out var image)) _images[texture] = image = (Image)texture.GetImage().Duplicate();
            var pixel = point - rect.Position;
            if (image.GetPixel((int)pixel.X, (int)pixel.Y).A > 0) return id;
        }
        return null;
    }

    /// <summary>只改变本地目标和圆环，不提交移动或业务请求；重复选择保持播放进度。</summary>
    public void Select(Guid? id)
    {
        if (id is not null && !_views.ContainsKey(id.Value)) id = null;
        if (SelectedObjectId == id) return;
        SelectedObjectId = id;
        _selection.Stop();
        _selection.Hide();
        _selection.Reparent(id is { } selected ? _views[selected] : this, false);
        _selection.Position = Vector2.Zero;
        if (id is null) return;
        // 外圈覆盖目标宽度，避免树根完全遮住原始小画布；原脚底锚点随等比缩放保持。
        _selection.Scale = Vector2.One * Mathf.Max(1, _views[id.Value].Texture.GetWidth() / 69f);
        _selection.Show();
        _selection.Play();
    }

    /// <summary>在延迟释放世界前同步清理本地反馈。</summary>
    public void ClearInteraction()
    {
        InteractionEnabled = false;
        _pointerPosition = new Vector2(-10000, -10000);
        Select(null);
    }

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
                if (SelectedObjectId == id) Select(null);
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

    /// <summary>释放仅供命中的 CPU 图像副本。</summary>
    public override void _ExitTree()
    {
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear();
    }
}
