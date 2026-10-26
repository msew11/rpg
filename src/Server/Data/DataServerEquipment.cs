using Rpg.Const;

namespace Rpg.Server.Data;

/// <summary>角色穿戴的一份物品，沿用背包稳定编号，数量固定为一。</summary>
/// <param name="InstanceId">角色内不复用的实例编号。</param>
/// <param name="ItemId">静态道具编号。</param>
/// <param name="Slot">当前占用的装备槽位。</param>
public sealed record DataServerEquipment(long InstanceId, int ItemId, EquipmentSlot Slot);
