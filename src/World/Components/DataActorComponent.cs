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
    /// <summary>外部显式动作或远端基础动作，由动作系统合成最终输出。</summary>
    public RoleMotion? RequestedMotion;
    /// <summary>显式动作请求的重播版本。</summary>
    public long RequestedMotionVersion;
    /// <summary>当前最终攻击动作的完整毫秒时长。</summary>
    public double AttackDurationMs;
    /// <summary>当前最终攻击动作已经过的毫秒数。</summary>
    public double AttackElapsedMs;
    /// <summary>已接纳的独立物品领域版本。</summary>
    public long EquipmentRevision;
    /// <summary>主手道具编号，零表示无装备。</summary>
    public int MainHandId;
    /// <summary>副手道具编号，零表示无装备。</summary>
    public int OffHandId;

    /// <summary>以站立动作、默认朝向和初始动作版本构造组件。</summary>
    public DataActorComponent()
    {
        Motion = RoleMotion.Stand;
        Facing = new Vector2(1, 1);
        MotionVersion = 0;
        RequestedMotion = null;
        RequestedMotionVersion = 0;
        AttackDurationMs = 0;
        AttackElapsedMs = 0;
        EquipmentRevision = -1;
        MainHandId = 0;
        OffHandId = 0;
    }
}
