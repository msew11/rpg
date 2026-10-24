using Godot;

namespace Rpg.UI;

/// <summary>全页面硬件指针：放大显示，左键按下播放两帧并在释放后复原。</summary>
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
        Present(_normal);
    }

    /// <summary>在 GUI 消费前观察左键，包括背包与登录按钮。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
        IsPressed = click.Pressed;
        _elapsed = 0;
        Present(IsPressed ? _pressed[0] : _normal);
    }

    /// <summary>按下一次播放完整两帧，保持末帧直到释放。</summary>
    public override void _Process(double delta)
    {
        if (!IsPressed) return;
        _elapsed += delta;
        Present(_pressed[_elapsed >= FrameSeconds ? 1 : 0]);
    }

    /// <summary>失去焦点时复原，避免窗口外释放后残留按下状态。</summary>
    public override void _Notification(int what)
    {
        if (what != NotificationApplicationFocusOut || _normal is null) return;
        IsPressed = false;
        Present(_normal);
    }

    /// <summary>退出时恢复系统默认指针。</summary>
    public override void _ExitTree() => Input.SetCustomMouseCursor(null);

    /// <summary>只在帧变化时更新硬件鼠标，指尖热点与画布同比例缩放。</summary>
    private void Present(Texture2D texture)
    {
        if (CurrentTexture == texture) return;
        CurrentTexture = texture;
        Input.SetCustomMouseCursor(texture, Input.CursorShape.Arrow, new Vector2(2, 2) * DisplayScale);
    }

    /// <summary>按最近邻缩放完整画布，保留现有像素素材边缘。</summary>
    private static Texture2D LoadFrame(string name)
    {
        var image = GD.Load<Texture2D>($"res://assets/pointer/{name}.png").GetImage();
        image.Resize(Mathf.RoundToInt(image.GetWidth() * DisplayScale), Mathf.RoundToInt(image.GetHeight() * DisplayScale), Image.Interpolation.Nearest);
        return ImageTexture.CreateFromImage(image);
    }
}
