using Godot;

namespace Rpg.World;

/// <summary>在地图逻辑坐标和 2:1 等轴画布坐标间转换。</summary>
public static class IsometricProjection
{
    /// <summary>把地图逻辑位置映射到等轴画布。</summary>
    public static Vector2 ToScreen(Vector2 logical) =>
        new((logical.X - logical.Y) * 0.5f, (logical.X + logical.Y) * 0.25f);

    /// <summary>把等轴画布位置还原为地图逻辑位置。</summary>
    public static Vector2 ToLogical(Vector2 screen) =>
        new(screen.X + 2 * screen.Y, 2 * screen.Y - screen.X);
}
