using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Utils;

namespace Rpg.Session.Data;

/// <summary>只持有配置编号及本地展示推导结果的公开快照。</summary>
public sealed class RoleWorldEquipmentPbData
{
    /// <summary>穿戴者身份。</summary>
    public long RoleId { get; }
    /// <summary>最近接纳的信封版本，不属于业务协议字段。</summary>
    public long Revision { get; }
    /// <summary>公开的 item.json 配置编号。</summary>
    public IReadOnlyList<int> EquippedItems { get; }
    /// <summary>从配置推导的展示位置，不持有实例编号。</summary>
    public IReadOnlyList<EquipmentDisplayData> Items { get; }

    /// <summary>保存经完整校验且由本对象拥有的列表。</summary>
    private RoleWorldEquipmentPbData(long roleId, long revision, List<EquipmentDisplayData> items)
    {
        RoleId = roleId;
        Revision = revision;
        Items = items.AsReadOnly();
        EquippedItems = Array.AsReadOnly(items.Select(item => item.ItemId).ToArray());
    }

    /// <summary>本地初始空展示；不用于容忍协议缺失。</summary>
    internal static RoleWorldEquipmentPbData Empty(long roleId) => new(roleId, 0, new());

    /// <summary>仅从公开配置编号验证槽位唯一性与双手规则。</summary>
    public static RoleWorldEquipmentPbData FromPb(RoleWorldEquipmentPb message,
        IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message is null || message.RoleId <= 0 || message.GetRevision() < 0)
            throw new InvalidDataException("装备协议缺少有效身份或信封版本");
        var items = message.EquippedItems.Select(id => EquipmentDisplayData.FromItemId(id, definitions))
            .OrderBy(item => item.Slot).ToList();
        if (items.Select(item => item.Slot).Distinct().Count() != items.Count)
            throw new InvalidDataException("装备展示包含重复槽位");
        var main = items.SingleOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        if (main is not null && items.Any(item => item.Slot == EquipmentSlot.OffHand))
        {
            var cfg = definitions[main.ItemId];
            if (EquipmentSlotUtil.OccupiesBothHands(cfg.Category, cfg.SubCategory))
                throw new InvalidDataException("双手武器不能同时装备副手物品");
        }
        return new(message.RoleId, message.GetRevision(), items);
    }

    /// <summary>忽略消息版本比较实际公开展示内容。</summary>
    public bool HasSameItems(RoleWorldEquipmentPbData other) => RoleId == other.RoleId && Items.SequenceEqual(other.Items);

    /// <summary>本人公开展示须恰好等于私有完整物品中的穿戴实例。</summary>
    public void ValidateBag(BagPbData bag)
    {
        if (!EquippedItems.OrderBy(id => id).SequenceEqual(bag.Equipment.Select(item => item.ItemId).OrderBy(id => id)))
            throw new InvalidDataException("公开装备与私有穿戴实例不一致");
    }

    /// <summary>只输出身份和配置编号，不输出私有实例或推导槽位。</summary>
    public RoleWorldEquipmentPb ToPb()
    {
        var message = new RoleWorldEquipmentPb { RoleId = RoleId };
        message.EquippedItems.AddRange(EquippedItems);
        return message.WithRevision(Revision);
    }
}
