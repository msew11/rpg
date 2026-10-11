using Godot;

/// <summary>保存实体在逻辑世界中的连续位置。</summary>
[GlobalClass]
public partial class PositionComponent : EcsComponent
{
    /// <summary>实体在逻辑世界中的连续位置。</summary>
    public Vector2 value { get; set; }

    /// <summary>设置初始逻辑位置。</summary>
    public PositionComponent(Vector2 initial = default) => value = initial;
}
