using Rpg.Const;

namespace Rpg.Session.Data;

/// <summary>从私有已穿物品和静态配置推导的 UI 操作数据。</summary>
/// <param name="InstanceId">私有物品实例编号。</param>
/// <param name="ItemId">道具配置编号。</param>
/// <param name="Slot">客户端按分类推导的槽位。</param>
public sealed record EquippedItemData(long InstanceId, int ItemId, EquipmentSlot Slot);
