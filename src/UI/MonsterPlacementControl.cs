using Godot;
using Rpg.Config.Data;
using Rpg.World.Utils;

namespace Rpg.UI;

/// <summary>在世界画布上显示与实物等大的放置虚影；右键取消从不发送协议。</summary>
public partial class MonsterPlacementControl : Node2D
{
    /// <summary>当前待放置配置，空值表示普通输入状态。</summary>
    public MonsterCfg? Selected { get; private set; }
    /// <summary>虚影当前地面锚点的逻辑坐标，也是最终 GM 使用的坐标。</summary>
    public Vector2 LogicalPosition { get; private set; }
    /// <summary>最新输入事件的视口坐标，供相机变化时重新投影。</summary>
    private Vector2 _pointerPosition;
    /// <summary>使用实物原始尺寸与地面锚点的透明虚影。</summary>
    private Sprite2D _preview = null!;

    /// <summary>建立共享世界相机缩放的虚影，保持在普通地图对象上方。</summary>
    public override void _Ready()
    {
        ZIndex = 30;
        _preview = new Sprite2D { Name = "MonsterPlacementPreview", Centered = false,
            Modulate = new Color(1, 1, 1, 0.6f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, Visible = false };
        AddChild(_preview);
    }

    /// <summary>选择配置只显示与实物同贴图、同尺寸、同锚点的虚影，不接触服务器。</summary>
    public void Begin(MonsterCfg config)
    {
        Selected = config;
        _preview.Texture = ResourceLoader.Load<Texture2D>(config.TexturePath);
        _preview.Offset = -new Vector2(config.AnchorX, config.AnchorY);
        UpdatePointer(GetViewport().GetMousePosition());
        _preview.Show();
    }

    /// <summary>统一计算虚影和请求的逻辑落点，再按实物相同的投影定位贴图。</summary>
    public void UpdatePointer(Vector2 viewportPosition)
    {
        _pointerPosition = viewportPosition;
        LogicalPosition = IsometricProjection.ToLogical(GetCanvasTransform().AffineInverse() * viewportPosition);
        _preview.GlobalPosition = IsometricProjection.ToScreen(LogicalPosition);
    }

    /// <summary>鼠标静止时也随相机平移和缩放重新计算锚点，避免虚影偏离指针。</summary>
    public override void _Process(double delta)
    {
        if (Selected is not null) UpdatePointer(_pointerPosition);
    }

    /// <summary>指针输入立即更新虚影，右键优先于 GUI 取消，即使鼠标停在窗口上也有效。</summary>
    public override void _Input(InputEvent input)
    {
        if (Selected is null) return;
        if (input is InputEventMouse mouse) UpdatePointer(mouse.Position);
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>确认、取消或离开世界共用同一本地状态清理。</summary>
    public void Cancel()
    {
        Selected = null;
        if (_preview is not null) _preview.Hide();
    }

    /// <summary>离树不保留怪物选择。</summary>
    public override void _ExitTree() => Cancel();
}
