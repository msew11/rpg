using Godot;

namespace Rpg.World.Views;

/// <summary>单个身体动作的表现资源，包含五方向帧和原始定位点。</summary>
[GlobalClass]
public partial class DataActorAnimation : Resource
{
    /// <summary>以 dir0 至 dir4 命名的五个原始方向动画。</summary>
    [Export] public SpriteFrames Frames { get; set; } = null!;
    /// <summary>图像左上角到角色定位点的偏移。</summary>
    [Export] public Vector2 Anchor { get; set; }
    /// <summary>按方向、帧顺序排列的身体画布头部挂接点。</summary>
    [Export] public Vector2[] HeadAttachments { get; set; } = [];
}
