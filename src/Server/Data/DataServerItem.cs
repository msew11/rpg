namespace Rpg.Server.Data;

/// <summary>服务端独立保存的道具实例，堆叠与装备均使用实例编号。</summary>
/// <param name="InstanceId">角色内唯一实例编号。</param>
/// <param name="ItemId">静态道具配置编号。</param>
/// <param name="SlotIndex">从零开始的背包格子。</param>
/// <param name="Count">本实例的堆叠数量。</param>
public sealed record DataServerItem(long InstanceId, int ItemId, int SlotIndex, int Count);
