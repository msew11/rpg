using Godot;

/// <summary>保存实体最后一次非零移动朝向。</summary>
[GlobalClass]
public partial class FacingComponent : EcsComponent
{
    /// <summary>实体最后一次非零移动的方向。</summary>
    public Vector2I value { get; set; } = new(0, 1);
}
