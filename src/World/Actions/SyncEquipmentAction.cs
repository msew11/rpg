namespace Rpg.World.Actions;

/// <summary>将已接纳物品版本的双手外观提交给世界，不携带资源或可写物品。</summary>
/// <param name="RoleId">快照所属角色。</param>
/// <param name="Revision">独立物品领域版本。</param>
/// <param name="MainHandId">主手道具编号。</param>
/// <param name="OffHandId">副手道具编号。</param>
public sealed record SyncEquipmentAction(long RoleId, long Revision, int MainHandId, int OffHandId) : WorldAction;
