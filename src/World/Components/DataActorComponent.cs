using Godot;
using Rpg.Const;

namespace Rpg.World.Components;

/// <summary>实体的动作和朝向逻辑，与资源和动画播放无关。</summary>
internal sealed class DataActorComponent
{
    /// <summary>当前由世界决定的动作。</summary>
    public RoleMotion Motion;
    /// <summary>最近的逻辑朝向；初始朝向投影后向下。</summary>
    public Vector2 Facing;
    /// <summary>显式动作请求版本，使相同动作可以再次播放。</summary>
    public long MotionVersion;

    /// <summary>以站立动作、默认朝向和初始动作版本构造组件。</summary>
    public DataActorComponent()
    {
        Motion = RoleMotion.Stand;
        Facing = new Vector2(1, 1);
        MotionVersion = 0;
    }
}
