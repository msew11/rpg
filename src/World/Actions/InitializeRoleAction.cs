using Rpg.Session.Data;

namespace Rpg.World.Actions;

/// <summary>初始化选中角色实体的消息。</summary>
public sealed record InitializeRoleAction(RoleWorldStatePbData Role) : WorldAction;
