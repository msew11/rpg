using System;
using Godot;

/// <summary>提供 C# 世界系统所需的区块坐标和等轴移动换算。</summary>
internal static class WorldMath
{
    /// <summary>将连续世界位置映射到所在的整数地块。</summary>
    internal static Vector2I TileAt(Vector2 position) => new(Mathf.FloorToInt(position.X), Mathf.FloorToInt(position.Y));

    /// <summary>求地块所在的行走区块，兼容负坐标。</summary>
    internal static Vector2I RawChunk(Vector2I tile, Vector2I chunkSize) =>
        new((int)Math.Floor((double)tile.X / chunkSize.X), (int)Math.Floor((double)tile.Y / chunkSize.Y));

    /// <summary>求地块在区块内的非负坐标。</summary>
    internal static Vector2I LocalTile(Vector2I tile, Vector2I chunkSize) =>
        new(PositiveMod(tile.X, chunkSize.X), PositiveMod(tile.Y, chunkSize.Y));

    /// <summary>将行走区块映射到有限世界中的规范区块。</summary>
    internal static Vector2I CanonicalChunk(Vector2I raw, Vector2I worldSize) =>
        new(PositiveMod(raw.X, worldSize.X), PositiveMod(raw.Y, worldSize.Y));

    /// <summary>把屏幕方向输入转换为等速的世界移动方向。</summary>
    internal static Vector2 MovementDirection(Vector2I screenInput) =>
        new Vector2(screenInput.X + screenInput.Y, screenInput.Y - screenInput.X).Normalized();

    /// <summary>返回除数为正时的非负余数。</summary>
    private static int PositiveMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
