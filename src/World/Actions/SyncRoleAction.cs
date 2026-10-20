using Rpg.Session;

namespace Rpg.World.Actions;

/// <summary>由会话缓存转来的权威角色状态消息。</summary>
public sealed record SyncRoleAction(DataRoleState State) : WorldAction;
