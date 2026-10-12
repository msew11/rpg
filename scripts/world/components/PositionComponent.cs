using Godot;
using System;
using System.Globalization;

/// <summary>保存实体在逻辑世界中的连续位置。</summary>
[GlobalClass]
public partial class PositionComponent : EcsComponent
{
    /// <summary>实体在逻辑世界中的连续位置。</summary>
    public Vector2 value { get; set; }

    /// <summary>设置初始逻辑位置。</summary>
    public PositionComponent(Vector2 initial) => value = initial;

    /// <summary>供落地恢复创建空位置组件。</summary>
    public PositionComponent() : this(Vector2.Zero) { }

    /// <summary>把连续位置转换为与区域设置无关的字符串。</summary>
    public override string ToPersistedString() =>
        $"{value.X.ToString("R", CultureInfo.InvariantCulture)},{value.Y.ToString("R", CultureInfo.InvariantCulture)}";

    /// <summary>从落地字符串恢复连续位置。</summary>
    public override void FromPersistedString(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 2)
            throw new FormatException("Invalid position data.");
        value = new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
