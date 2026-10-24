using Godot;
using Rpg.Const;

namespace Rpg.World;

/// <summary>世界对表现层提供的不可变玩家快照；方向使用地图逻辑坐标。</summary>
/// <param name="HeadId">表现头型编号，0 兼容旧版默认头型。</param>
public sealed record DataActorState(long RoleId, RoleGender Gender, Vector2 Position,
    RoleMotion Motion, Vector2 Facing, long MotionVersion, int HeadId = 0);
