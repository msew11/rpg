using Godot;

namespace Rpg.UI;

/// <summary>首期使用的中性角色头像占位图。</summary>
public partial class AvatarControl : Control
{
    /// <summary>绘制头像轮廓以验证 HUD 布局。</summary>
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("314c5b"));
        DrawCircle(new Vector2(32, 23), 13, new Color("e9c684"));
        DrawRect(new Rect2(14, 39, 36, 22), new Color("7895a4"));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("89aab6"), false, 2);
    }
}
