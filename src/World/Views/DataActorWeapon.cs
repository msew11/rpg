using Godot;

namespace Rpg.World.Views;

/// <summary>独立武器动作的五方向纹理、剑柄连接点和相对身体遮挡层级。</summary>
[GlobalClass]
public partial class DataActorWeapon : Resource
{
    /// <summary>按方向、帧顺序排列的纹理，每个方向帧数相同。</summary>
    [Export] public Texture2D[] Frames { get; set; } = [];
    /// <summary>每帧剑柄在原始画布内的连接坐标。</summary>
    [Export] public Vector2[] Anchors { get; set; } = [];
    /// <summary>每帧相对身体的绘制层级，负值在后，非负值在前。</summary>
    [Export] public int[] Layers { get; set; } = [];
}
