using Godot;
using System;
using System.Globalization;

/// <summary>保存实体最后一次非零移动朝向。</summary>
[GlobalClass]
public partial class FacingComponent : EcsComponent
{
    /// <summary>实体最后一次非零移动的方向。</summary>
    public Vector2I value { get; set; } = new(0, 1);

    /// <summary>把离散朝向转换为字符串。</summary>
    public override string ToPersistedString() => $"{value.X},{value.Y}";

    /// <summary>从落地字符串恢复离散朝向。</summary>
    public override void FromPersistedString(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 2)
            throw new FormatException("Invalid facing data.");
        value = new Vector2I(int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
