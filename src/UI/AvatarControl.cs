using Godot;

namespace Rpg.UI;

/// <summary>绘制当前角色头像及左下角等级徽标。</summary>
public partial class AvatarControl : Control
{
    /// <summary>与角色头型对应的头像图。</summary>
    public Texture2D? Portrait { get; set; }

    /// <summary>角色当前等级。</summary>
    public int Level { get; set; }

    /// <summary>在固定头像区域绘制图片、边框和等级。</summary>
    public override void _Draw()
    {
        var center = new Vector2(36, 36);
        DrawCircle(center, 35, new Color("182630"));
        if (Portrait is not null)
            DrawTextureRect(Portrait, new Rect2(4, 4, 64, 64), false);
        DrawArc(center, 34, 0, Mathf.Tau, 64, new Color("87a8b1"), 2, true);
        DrawCircle(new Vector2(13, 60), 12, new Color("192a34"));
        DrawArc(new Vector2(13, 60), 12, 0, Mathf.Tau, 32, new Color("87a8b1"), 1.5f, true);
        DrawString(ThemeDB.FallbackFont, new Vector2(13, 65), Level.ToString(),
            HorizontalAlignment.Center, 22, 13, Colors.White);
    }
}
