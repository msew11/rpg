using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>与可变协议对象隔离的不可变道具实例。</summary>
/// <param name="InstanceId">角色内实例编号。</param>
/// <param name="ItemId">静态道具配置编号。</param>
/// <param name="SlotIndex">从零开始的格子编号。</param>
/// <param name="Count">当前堆叠数量。</param>
public sealed record ItemPbData(long InstanceId, int ItemId, int SlotIndex, int Count)
{
    /// <summary>复制并验证基础实例字段。</summary>
    public static ItemPbData FromPb(ItemPb message)
    {
        if (message is null) throw new InvalidDataException("物品协议不能为空");
        var data = new ItemPbData(message.InstanceId, message.ItemId, message.SlotIndex, message.Count);
        data.Validate();
        return data;
    }

    /// <summary>输出新的协议对象，不共享可变引用。</summary>
    public ItemPb ToPb()
    {
        Validate();
        return new ItemPb { InstanceId = InstanceId, ItemId = ItemId, SlotIndex = SlotIndex, Count = Count };
    }

    /// <summary>拒绝非法编号、位置或数量。</summary>
    private void Validate()
    {
        if (InstanceId <= 0 || ItemId <= 0 || SlotIndex < 0 || Count <= 0)
            throw new InvalidDataException("物品实例字段无效");
    }
}
