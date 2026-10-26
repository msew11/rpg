using Godot;

namespace Rpg.World.Views;

/// <summary>独立装备动作的五方向纹理、持握连接点和相对身体遮挡层级。</summary>
[GlobalClass]
public partial class DataActorWeapon : Resource
{
    /// <summary>基础素材来自左手时使用的独立右手原画及连接点。</summary>
    [Export] public DataActorWeapon? RightHandVariant { get; set; }
    /// <summary>独立左手原画及连接点；未提供时沿用本资源的固定持握素材。</summary>
    [Export] public DataActorWeapon? LeftHandVariant { get; set; }
    /// <summary>按方向、帧顺序排列的纹理，每个方向帧数相同。</summary>
    [Export] public Texture2D[] Frames { get; set; } = [];
    /// <summary>长柄握点后的独立帧，与前段同步并随方向切换身体前后关系；普通装备为空。</summary>
    [Export] public Texture2D[] RearFrames { get; set; } = [];
    /// <summary>每帧持握位置在原始画布内的连接坐标。</summary>
    [Export] public Vector2[] Anchors { get; set; } = [];
    /// <summary>原武器每帧的层次偏置，由实际持握手和朝向决定最终前后遮挡。</summary>
    [Export] public int[] Layers { get; set; } = [];
}
