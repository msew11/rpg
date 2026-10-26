using System;
using Godot;
using Rpg.Const;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.UI;

/// <summary>选角、创建与装备窗口共用的独立站立预览、八方向旋转及下方操作区。</summary>
public partial class RolePreviewControl : VBoxContainer
{
    /// <summary>装备窗口隐藏独立头像，选角与创角保持默认显示。</summary>
    public bool ShowPortrait { get; init; } = true;
    /// <summary>预览人物相对原始地图素材的比例。</summary>
    public float ActorScale { get; init; } = 2.4f;
    /// <summary>脚底定位点到预览区底边的距离，为素材脚部预留空间。</summary>
    public float ActorBottomMargin { get; init; } = 12;
    /// <summary>所有人物展示默认朝屏幕右下方（↘）。</summary>
    public const int DefaultDirection = 1;
    /// <summary>当前预览朝向，用户旋转后保留展示状态。</summary>
    public int Direction { get; private set; } = DefaultDirection;
    /// <summary>供页面记录当前草稿朝向。</summary>
    public event Action<int>? DirectionChanged;
    /// <summary>预览下方的页面操作区。</summary>
    public VBoxContainer Actions { get; } = new() { Name = "PreviewActions" };
    /// <summary>预览当前角色的名字或创建说明。</summary>
    private readonly Label _title = new() { Name = "PreviewTitle", HorizontalAlignment = HorizontalAlignment.Center };
    /// <summary>角色独立绘制区域。</summary>
    private readonly Control _stage = new() { Name = "Stage", SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
    /// <summary>复用分层角色表现，不持有 ECS 世界。</summary>
    private readonly ActorView _actor = new() { Name = "PreviewActor" };
    /// <summary>与游戏 HUD 同素材的无等级圆形头像。</summary>
    private readonly AvatarControl _portrait = new() { Name = "PreviewPortrait", ShowLevel = false,
        CustomMinimumSize = new Vector2(72, 72), Scale = new Vector2(0.9f, 0.9f),
        MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    /// <summary>上次展示的不可变外观草稿。</summary>
    private DataActorState? _state;

    /// <summary>建立预览与旋转区，页面操作始终位于预览下方。</summary>
    public override void _Ready()
    {
        AddChild(_title);
        AddChild(_stage);
        _actor.Scale = Vector2.One * ActorScale;
        _stage.AddChild(_actor);
        _stage.AddChild(_portrait);
        var rotation = new HBoxContainer { Name = "PreviewRotation", Alignment = BoxContainer.AlignmentMode.Center };
        rotation.AddChild(RotationButton("counterclockwise", "RotateCounterclockwise", () => Rotate(-1)));
        rotation.AddChild(RotationButton("clockwise", "RotateClockwise", () => Rotate(1)));
        AddChild(rotation);
        AddChild(Actions);
        _stage.Resized += Present;
        Present();
    }

    /// <summary>选择外观并恢复可选的草稿朝向，空角色列表可清除预览。</summary>
    public void ShowRole(RoleGender gender, int headId, long roleId, string title, int? direction = null,
        int mainHandId = 0, int offHandId = 0)
    {
        if (direction is { } value) Direction = Mathf.PosMod(value, 8);
        _state = new DataActorState(roleId, gender, Vector2.Zero, RoleMotion.Stand, Vector2.Zero, 0, headId, mainHandId, offHandId);
        _title.Text = title;
        _actor.Visible = true;
        _portrait.Portrait = ShowPortrait ? AvatarControl.LoadPortrait(gender, headId) : null;
        _portrait.Visible = ShowPortrait;
        _portrait.QueueRedraw();
        Present();
    }

    /// <summary>空列表不显示虚构角色，但保留页面操作入口。</summary>
    public void ClearRole()
    {
        _state = null;
        _actor.Visible = false;
        _portrait.Visible = false;
        _title.Text = "暂无角色";
    }

    /// <summary>每次按屏幕坐标系顺逆转动 45 度并记录当前草稿。</summary>
    private void Rotate(int step)
    {
        // 人物绕自身竖轴的旋转方向与屏幕朝向角度增量相反。
        Direction = Mathf.PosMod(Direction - step, 8);
        DirectionChanged?.Invoke(Direction);
        Present();
    }

    /// <summary>在预览区定位脚底，头身显示不创建或移动世界实体。</summary>
    private void Present()
    {
        if (!IsNodeReady() || _state is null) return;
        _actor.Present(_state with
        {
            Position = IsometricProjection.ToLogical(new Vector2(_stage.Size.X / 2, _stage.Size.Y - ActorBottomMargin)),
            Facing = IsometricProjection.ToLogical(Vector2.FromAngle(Direction * Mathf.Pi / 4))
        });
        _portrait.Position = new Vector2(Mathf.Max(12, _stage.Size.X / 2 - 120),
            Mathf.Max(12, _stage.Size.Y - 280));
    }

    /// <summary>创建页面中尺寸一致且可被验收定位的操作按钮。</summary>
    public static Button ActionButton(string text, string name, Action action)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 40) };
        button.Pressed += action;
        return button;
    }

    /// <summary>所有预览共用方向明确的箭头图标，保留原按钮标识及音效入口。</summary>
    private static Button RotationButton(string icon, string name, Action action)
    {
        var button = ActionButton("", name, action);
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/rotate-{icon}.svg");
        button.CustomMinimumSize = new Vector2(28, 28);
        button.IconAlignment = HorizontalAlignment.Center;
        return button;
    }
}
