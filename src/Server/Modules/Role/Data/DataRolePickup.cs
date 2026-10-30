namespace Rpg.Server.Modules.Role.Data;

/// <summary>当前角色尚待到达的拾取意图，不保存版本或取消队列。</summary>
internal sealed record DataRolePickup(string ObjectId, ulong MoveActionId);
