using System;
using Godot;
using Rpg.Session.Data;
using Rpg.World.Utils;

namespace Rpg.World.Views;

/// <summary>在地图画布上标出客户端已接纳的权威 AOI 块。</summary>
public partial class AoiHighlightView : Node2D
{
    /// <summary>周围块的半透明填充。</summary>
    private static readonly Color NeighborFill = new(0.05f, 0.76f, 1f, 0.11f);
    /// <summary>中心块的较深填充。</summary>
    private static readonly Color CenterFill = new(0.05f, 0.76f, 1f, 0.24f);
    /// <summary>周围块的青蓝边线。</summary>
    private static readonly Color NeighborLine = new(0.15f, 0.85f, 1f, 0.8f);
    /// <summary>中心块的醒目边线。</summary>
    private static readonly Color CenterLine = new(0.15f, 0.95f, 1f);
    /// <summary>画布倍率，用于在 GM 缩放时维持可见线宽。</summary>
    private float _screenScale = 1f;
    /// <summary>当前已验证的地图订阅快照，空值表示没有可显示的世界。</summary>
    public DataMapAoiSnapshotPbData? Snapshot { get; private set; }

    /// <summary>只在客户端接纳的新快照到达时更新绘图。</summary>
    public void SetSnapshot(DataMapAoiSnapshotPbData? value)
    {
        if (ReferenceEquals(Snapshot, value)) return;
        Snapshot = value;
        if (Visible) QueueRedraw();
    }

    /// <summary>相机缩放变化时保持边线约两屏幕像素。</summary>
    public void SetScreenScale(float value)
    {
        if (Mathf.IsEqualApprox(_screenScale, value)) return;
        _screenScale = value;
        if (Visible) QueueRedraw();
    }

    /// <summary>GM 开关只控制表现，不触碰订阅缓存。</summary>
    public void SetHighlightVisible(bool visible) => Visible = visible;

    /// <summary>从已接纳块坐标计算地图内逻辑像素范围，边缘块按地图规格截断。</summary>
    internal static Rect2 ChunkRect(DataMapAoiSnapshotPbData snapshot, (int X, int Y) coordinate)
    {
        var layout = snapshot.Layout;
        var leftTile = coordinate.X * layout.ChunkSizeTiles;
        var topTile = coordinate.Y * layout.ChunkSizeTiles;
        var rightTile = Math.Min(leftTile + layout.ChunkSizeTiles, layout.Width);
        var bottomTile = Math.Min(topTile + layout.ChunkSizeTiles, layout.Height);
        var size = layout.TileSize;
        return new Rect2(leftTile * size, topTile * size,
            (rightTile - leftTile) * size, (bottomTile - topTile) * size);
    }

    /// <summary>先绘周围块、后绘中心块，使中心边线覆盖共享边。</summary>
    public override void _Draw()
    {
        if (!Visible || Snapshot is not { } snapshot) return;
        foreach (var coordinate in snapshot.Chunks)
            if (coordinate != snapshot.Center) DrawChunk(snapshot, coordinate, false);
        if (snapshot.Chunks.Contains(snapshot.Center)) DrawChunk(snapshot, snapshot.Center, true);
    }

    /// <summary>把逻辑矩形投影为等轴菱形并绘制填充及边线。</summary>
    private void DrawChunk(DataMapAoiSnapshotPbData snapshot, (int X, int Y) coordinate, bool center)
    {
        var rect = ChunkRect(snapshot, coordinate);
        var left = rect.Position.X; var top = rect.Position.Y;
        var right = rect.End.X; var bottom = rect.End.Y;
        var points = new[]
        {
            IsometricProjection.ToScreen(new Vector2(left, top)),
            IsometricProjection.ToScreen(new Vector2(right, top)),
            IsometricProjection.ToScreen(new Vector2(right, bottom)),
            IsometricProjection.ToScreen(new Vector2(left, bottom))
        };
        DrawColoredPolygon(points, center ? CenterFill : NeighborFill);
        DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] },
            center ? CenterLine : NeighborLine, (center ? 3f : 2f) / Mathf.Max(_screenScale, 0.01f), true);
    }
}
