using Godot;
using Rpg.Const;

namespace Rpg.World.Views;

/// <summary>完整原生武器动作的五方向纹理、连接点及来源动作类别。</summary>
[GlobalClass]
public partial class DataActorWeapon : Resource
{
    /// <summary>镜像前的源手别，整体镜像时保持不变。</summary>
    [Export] public bool SourceRightHand { get; set; } = true;
    /// <summary>实际来源动作编号，缺少动作时为站立1。</summary>
    [Export] public int SourceAction { get; set; } = 1;
    /// <summary>系列原生攻击类别，Stand表示盾牌不指定类别。</summary>
    [Export] public RoleMotion AttackMotion { get; set; }
    /// <summary>基础素材来自左手时使用的独立右手原画及连接点。</summary>
    [Export] public DataActorWeapon? RightHandVariant { get; set; }
    /// <summary>独立左手原画及连接点；未提供时沿用本资源的固定持握素材。</summary>
    [Export] public DataActorWeapon? LeftHandVariant { get; set; }
    /// <summary>按方向、帧顺序排列的纹理，每个方向帧数相同。</summary>
    [Export] public Texture2D[] Frames { get; set; } = [];
    /// <summary>每帧持握位置在原始画布内的连接坐标。</summary>
    [Export] public Vector2[] Anchors { get; set; } = [];
}
