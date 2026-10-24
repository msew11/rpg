using Godot;

namespace Rpg.World.Views;

/// <summary>独立头部五方向纹理及相对于各帧画布的脖颈连接点。</summary>
[GlobalClass]
public partial class DataActorHead : Resource
{
    /// <summary>顺序与身体 dir0 至 dir4 对应的头部帧，包含配套头发。</summary>
    [Export] public Texture2D[] Frames { get; set; } = [];
    /// <summary>每个方向连接点在头部画布内的位置。</summary>
    [Export] public Vector2[] Anchors { get; set; } = [];
}
