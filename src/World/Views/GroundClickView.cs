using Godot;

namespace Rpg.World.Views;

/// <summary>在地面点击世界坐标显示一次由小到大的光圈涟漪，淡出后自动释放。</summary>
public partial class GroundClickView : Sprite2D
{
    /// <summary>单帧光圈的可见时长，属于本项目的反馈表现参数。</summary>
    public const double FadeSeconds = 0.7;

    /// <summary>加载完整画布，围绕点击中心等比扩散并淡出，动画随节点清理。</summary>
    public override void _Ready()
    {
        Texture = GD.Load<Texture2D>("res://assets/pointer/circle.png");
        Centered = true;
        ZIndex = 1;
        TextureFilter = TextureFilterEnum.Nearest;
        Scale = Vector2.One * 0.25f;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "scale", Vector2.One, FadeSeconds)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0.0, FadeSeconds);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
