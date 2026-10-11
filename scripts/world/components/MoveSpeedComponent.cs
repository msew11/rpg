using Godot;

/// <summary>保存实体在逻辑世界中的移动速度。</summary>
[GlobalClass]
public partial class MoveSpeedComponent : EcsComponent
{
    /// <summary>实体每秒在逻辑世界中的移动距离。</summary>
    public float value { get; set; }

    /// <summary>设置初始移动速度。</summary>
    public MoveSpeedComponent(float initial = 0) => value = initial;
}
