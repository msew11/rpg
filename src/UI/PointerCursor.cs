using Godot;
using System.Linq;

namespace Rpg.UI;

/// <summary>全页面硬件指针：普通点击动画与 HUD 开手、握手反馈。</summary>
public partial class PointerCursor : Node
{
    /// <summary>普通与按下帧统一放大比例，热点同步缩放。</summary>
    private const float DisplayScale = 1.25f;
    /// <summary>按下动画每帧秒数，播放一次后保持末帧。</summary>
    private const double FrameSeconds = 0.08;
    /// <summary>普通硬件指针纹理。</summary>
    private Texture2D _normal = null!;
    /// <summary>按下反馈的两帧完整画布。</summary>
    private Texture2D[] _pressed = [];
    /// <summary>HUD 悬停与握住使用的 Fetch 两帧。</summary>
    private Texture2D[] _fetch = [];
    /// <summary>当前是否以开手提示可拖动 HUD。</summary>
    public bool IsHudHovering { get; private set; }
    /// <summary>当前是否保持 HUD 握手拖动。</summary>
    public bool IsHudDragging { get; private set; }
    /// <summary>当前硬件指针的缩放后热点。</summary>
    public Vector2 CurrentHotspot { get; private set; }
    /// <summary>应用失焦期间禁止恢复悬停状态。</summary>
    private bool _focused = true;
    /// <summary>左键是否仍保持按下。</summary>
    public bool IsPressed { get; private set; }
    /// <summary>实际交给硬件鼠标的当前纹理。</summary>
    public Texture2D CurrentTexture { get; private set; } = null!;
    /// <summary>按下状态累计播放时间。</summary>
    private double _elapsed;

    /// <summary>加载并统一缩放素材，在输入分发开始前启用普通指针。</summary>
    public override void _Ready()
    {
        _normal = LoadFrame("normal");
        _pressed = [LoadFrame("click_0"), LoadFrame("click_1")];
        _fetch = [LoadFrame("fetch_0"), LoadFrame("fetch_1")];
        Present(_normal);
    }

    /// <summary>在 GUI 消费前观察左键，包括背包与登录按钮。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
        var hudActive = RefreshHudCursor();
        IsPressed = click.Pressed;
        _elapsed = 0;
        if (!hudActive) Present(IsPressed ? _pressed[0] : _normal);
    }

    /// <summary>优先呈现 HUD 抓取；普通点击播放两帧并保持末帧至释放。</summary>
    public override void _Process(double delta)
    {
        if (RefreshHudCursor() || !IsPressed) return;
        _elapsed += delta;
        Present(_pressed[_elapsed >= FrameSeconds ? 1 : 0]);
    }

    /// <summary>失去焦点时复原，避免窗口外释放后残留按下状态。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn) { _focused = true; return; }
        if (what != NotificationApplicationFocusOut || _normal is null) return;
        _focused = false;
        IsHudHovering = false;
        IsHudDragging = false;
        IsPressed = false;
        Present(_normal);
    }

    /// <summary>优先呈现覆盖框悬停和拖动状态，拖出范围仍保持握手。</summary>
    private bool RefreshHudCursor()
    {
        var dragging = _focused && GetTree().GetNodesInGroup("HudEditOverlays")
            .OfType<HudEditOverlay>().Any(overlay => overlay.IsVisibleInTree() && overlay.Dragging);
        var hovering = _focused && GetViewport().GuiGetHoveredControl() is HudEditOverlay overlay && overlay.IsVisibleInTree();
        var previous = IsHudDragging || IsHudHovering;
        IsHudDragging = dragging;
        IsHudHovering = hovering;
        if (dragging || hovering)
        {
            Present(_fetch[dragging ? 1 : 0], new Vector2(6, 6) * DisplayScale);
            return true;
        }
        if (previous)
        {
            IsPressed = false;
            Present(_normal);
        }
        return false;
    }

    /// <summary>退出时恢复系统默认指针。</summary>
    public override void _ExitTree() => Input.SetCustomMouseCursor(null);

    /// <summary>只在帧变化时更新硬件鼠标，指尖热点与画布同比例缩放。</summary>
    private void Present(Texture2D texture, Vector2? hotspot = null)
    {
        var actualHotspot = hotspot ?? new Vector2(2, 2) * DisplayScale;
        if (CurrentTexture == texture && CurrentHotspot == actualHotspot) return;
        CurrentHotspot = actualHotspot;
        CurrentTexture = texture;
        Input.SetCustomMouseCursor(texture, Input.CursorShape.Arrow, actualHotspot);
    }

    /// <summary>按最近邻缩放完整画布，保留现有像素素材边缘。</summary>
    private static Texture2D LoadFrame(string name)
    {
        var image = GD.Load<Texture2D>($"res://assets/pointer/{name}.png").GetImage();
        image.Resize(Mathf.RoundToInt(image.GetWidth() * DisplayScale), Mathf.RoundToInt(image.GetHeight() * DisplayScale), Image.Interpolation.Nearest);
        return ImageTexture.CreateFromImage(image);
    }
}
