using Godot;

namespace Rpg.World.Views;

/// <summary>权威命中后独立播放的伤害文字，不依赖怪物节点存活。</summary>
public partial class DamageNumberView : Node2D
{
    /// <summary>显示整数伤害并向上飘动淡出。</summary>
    public void ShowDamage(int damage)
    {
        var label = new Label { Text = damage.ToString(), Position = new Vector2(-30, -90), Size = new Vector2(60, 30),
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", Colors.Yellow);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 5);
        AddChild(label);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", -135f, 0.8);
        tween.TweenProperty(label, "modulate:a", 0f, 0.8);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
