using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>完整且只读的背包快照，由整份协议验证后发布。</summary>
public sealed class BagPbData
{
    /// <summary>独立递增的背包版本。</summary>
    public long Revision { get; }
    /// <summary>已复制且不可增删改的物品实例。</summary>
    public IReadOnlyList<ItemPbData> Items { get; }
    /// <summary>只接收已经验证、由本类拥有的快照列表。</summary>
    private BagPbData(long revision, List<ItemPbData> items)
    {
        Revision = revision;
        Items = items.AsReadOnly();
    }

    /// <summary>检查容量、配置、堆叠和唯一性，成功后才构造完整快照。</summary>
    public static BagPbData FromPb(BagPb message, int capacity, IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message is null || message.Revision < 0 || capacity <= 0) throw new InvalidDataException("背包协议或容量无效");
        var items = message.Items.Select(ItemPbData.FromPb).ToList();
        if (items.Select(item => item.InstanceId).Distinct().Count() != items.Count ||
            items.Select(item => item.SlotIndex).Distinct().Count() != items.Count)
            throw new InvalidDataException("背包包含重复实例或格子");
        foreach (var item in items)
        {
            if (item.SlotIndex >= capacity || !definitions.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack)
                throw new InvalidDataException("背包物品越界、配置不存在或数量超出上限");
        }
        return new BagPbData(message.Revision, items);
    }

    /// <summary>创建全新的协议及实例集合。</summary>
    public BagPb ToPb()
    {
        var message = new BagPb { Revision = Revision };
        message.Items.AddRange(Items.Select(item => item.ToPb()));
        return message;
    }
}
