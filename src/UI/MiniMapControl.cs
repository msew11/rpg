using Godot;
using Rpg.Config;

namespace Rpg.UI;

/// <summary>根据配置尺寸与会话位置绘制世界外的小地图或大地图。</summary>
public partial class MiniMapControl : Control
{
    /// <summary>用于计算相对位置的地图配置。</summary>
    public DataMapConfig? Map { get; set; }
    /// <summary>会话缓存提供的权威角色位置。</summary>
    public Vector2 RolePosition { get; set; }

    /// <summary>绘制地图范围及角色位置。</summary>
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("1c303a"));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("7a9eaa"), false, 2);
        if (Map is null) return;
        var width = Map.Width * Map.TileSize;
        var height = Map.Height * Map.TileSize;
        var point = new Vector2(RolePosition.X / width * Size.X, RolePosition.Y / height * Size.Y);
        DrawCircle(point, 5, new Color("ffcc70"));
    }
}
