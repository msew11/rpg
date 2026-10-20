using Godot;

namespace Rpg.World.Views;

/// <summary>只绘制当前角色，不持有或改变 ECS 状态。</summary>
public partial class ActorView : Node2D
{
    /// <summary>绘制中性占位角色与方向标记。</summary>
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 13, new Color("f1c979"));
        DrawArc(Vector2.Zero, 15, 0, Mathf.Tau, 32, new Color("202a31"), 2);
        DrawCircle(new Vector2(4, -3), 2, new Color("202a31"));
    }
}
