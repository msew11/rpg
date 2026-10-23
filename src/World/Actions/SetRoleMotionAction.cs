using Rpg.Const;

namespace Rpg.World.Actions;

/// <summary>为指定玩家设置非移动动作；只表达姿态，不附带玩法效果。</summary>
public sealed record SetRoleMotionAction(long RoleId, RoleMotion Motion) : WorldAction;
