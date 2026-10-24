using Godot;

namespace Rpg.World.Views;

/// <summary>在地面点击世界坐标显示一次完整光圈，淡出后自动释放。</summary>
public partial class GroundClickView : Sprite2D
{
    /// <summary>单帧光圈的可见时长，属于本项目的反馈表现参数。</summary>
    public const double FadeSeconds = 0.5;

    /// <summary>加载完整画布并将淡出绑定到节点生命周期。</summary>
    public override void _Ready()
    {
        Texture = GD.Load<Texture2D>("res://assets/pointer/circle.png");
        Centered = true;
        ZIndex = 1;
        TextureFilter = TextureFilterEnum.Nearest;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0, FadeSeconds);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
