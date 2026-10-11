using Godot;

/// <summary>保存实体当前的屏幕移动方向输入。</summary>
[GlobalClass]
public partial class MovementIntentComponent : EcsComponent
{
    /// <summary>实体当前的屏幕方向输入。</summary>
    public Vector2I value { get; set; } = Vector2I.Zero;
}
