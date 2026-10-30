using System;
using System.Collections.Generic;
using System.Numerics;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Data;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>丢弃落点的纯几何验证与候选选择。</summary>
internal static class GroundDrop
{
    /// <summary>只验证客户端落点与权威角色位置、身体避让区及地图的关系。</summary>
    internal static Vector2 Validate(CreateGroundItem request, DataWorldLayout rules, Vector2 position)
    {
        var point = new Vector2(request.DropX, request.DropY);
        if (!Contains(rules, point.X, point.Y)) throw new ArgumentException("丢弃落点超出地图");
        var delta = point - position;
        var screenX = (delta.X - delta.Y) * 0.5f;
        var screenY = (delta.X + delta.Y) * 0.25f;
        var radiusSquared = screenX * screenX + screenY * screenY;
        // 移动确认的 3 逻辑像素修正及一帧同步差容许约 2 屏幕像素误差。
        if (!float.IsFinite(radiusSquared) || radiusSquared > 58f * 58f ||
            radiusSquared < 46f * 46f || (MathF.Abs(screenX) < 38f && screenY < 22f))
            throw new ArgumentException("丢弃落点不在角色周围的有效区域");
        return point;
    }

    /// <summary>兼容旧请求，在等轴画面周围选点；狭小地图使用裁剪后最远候选。</summary>
    internal static Vector2 Choose(DataWorldLayout rules, Vector2 position)
    {
        var candidates = new List<Vector2>();
        var farthest = position;
        var farthestDistance = -1f;
        for (var index = 0; index < 64; index++)
        {
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var radius = 48 + Random.Shared.NextSingle() * 8;
            var screen = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var point = position + new Vector2(screen.X + 2 * screen.Y, 2 * screen.Y - screen.X);
            var clamped = Vector2.Clamp(point, Vector2.Zero,
                new Vector2((long)rules.Width * rules.TileSize - 1, (long)rules.Height * rules.TileSize - 1));
            var delta = clamped - position;
            var projected = new Vector2((delta.X - delta.Y) * 0.5f, (delta.X + delta.Y) * 0.25f);
            if (projected.LengthSquared() > farthestDistance)
            {
                farthestDistance = projected.LengthSquared();
                farthest = clamped;
            }
            if (Contains(rules, point.X, point.Y) && (MathF.Abs(screen.X) >= 40 || screen.Y >= 24))
                candidates.Add(point);
        }
        return candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : farthest;
    }

    /// <summary>固定地图的逻辑像素边界。</summary>
    internal static bool Contains(DataWorldLayout rules, float x, float y) => float.IsFinite(x) && float.IsFinite(y) &&
        x >= 0 && y >= 0 && x < (long)rules.Width * rules.TileSize && y < (long)rules.Height * rules.TileSize;
}
