using Godot;

namespace Rpg.World.Views;

/// <summary>在实体原点绘制不参与输入的 GM 定位标记。</summary>
public partial class EntityAnchorView : Node2D
{
    /// <summary>标记颜色，默认黄色。</summary>
    public Color MarkerColor { get; private set; } = Colors.Yellow;

    /// <summary>让标记固定显示在实体贴图上方。</summary>
    public EntityAnchorView()
    {
        ZAsRelative = false;
        ZIndex = 100;
    }

    /// <summary>设置颜色并立即重绘。</summary>
    public void SetMarkerColor(Color color)
    {
        MarkerColor = color;
        QueueRedraw();
    }

    /// <summary>绘制带深色描边的十字定位点。</summary>
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 5, Colors.Black);
        DrawCircle(Vector2.Zero, 3, MarkerColor);
        DrawLine(new Vector2(-8, 0), new Vector2(-5, 0), MarkerColor, 2);
        DrawLine(new Vector2(5, 0), new Vector2(8, 0), MarkerColor, 2);
        DrawLine(new Vector2(0, -8), new Vector2(0, -5), MarkerColor, 2);
        DrawLine(new Vector2(0, 5), new Vector2(0, 8), MarkerColor, 2);
    }
}
