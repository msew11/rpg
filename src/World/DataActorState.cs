using Godot;
using Rpg.Session;

namespace Rpg.World;

/// <summary>世界对表现层提供的不可变玩家快照；方向使用地图逻辑坐标。</summary>
public sealed record DataActorState(long RoleId, RoleGender Gender, Vector2 Position,
    RoleMotion Motion, Vector2 Facing, long MotionVersion);
