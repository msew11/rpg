using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Config.Data;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>可独立转换和公开的不可变装备快照，不依赖背包及私有角色状态。</summary>
public sealed class RoleWorldEquipmentPbData
{
    /// <summary>穿戴者身份。</summary>
    public long RoleId { get; }
    /// <summary>仅穿戴内容变化时递增的版本。</summary>
    public long Revision { get; }
    /// <summary>已经复制并校验的已占用装备槽位。</summary>
    public IReadOnlyList<EquippedItemPbData> Items { get; }

    /// <summary>拥有经校验的独立集合。</summary>
    private RoleWorldEquipmentPbData(long roleId, long revision, List<EquippedItemPbData> items)
    {
        RoleId = roleId;
        Revision = revision;
        Items = items.AsReadOnly();
    }

    /// <summary>供本地纯数据构造使用的初始空装备，协议缺失不得走此退路。</summary>
    internal static RoleWorldEquipmentPbData Empty(long roleId) => new(roleId, 0, new());

    /// <summary>校验身份、版本、槽位适配和唯一性，完整复制后才返回。</summary>
    public static RoleWorldEquipmentPbData FromPb(RoleWorldEquipmentPb message,
        IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (message is null || message.RoleId <= 0 || message.Revision < 0)
            throw new InvalidDataException("装备协议缺少有效身份或版本");
        var items = message.Items.Select(item => EquippedItemPbData.FromPb(item, definitions))
            .OrderBy(item => item.Slot).ToList();
        if (items.Select(item => item.InstanceId).Distinct().Count() != items.Count ||
            items.Select(item => item.Slot).Distinct().Count() != items.Count)
            throw new InvalidDataException("装备包含重复实例或槽位");
        return new RoleWorldEquipmentPbData(message.RoleId, message.Revision, items);
    }

    /// <summary>比较规范排序后的完整装备，识别相同版本的内容冲突。</summary>
    public bool HasSameItems(RoleWorldEquipmentPbData other) => RoleId == other.RoleId && Items.SequenceEqual(other.Items);

    /// <summary>跨列表校验保证本人同一实例只处于背包或装备一侧。</summary>
    public void ValidateBag(BagPbData bag)
    {
        if (Items.Select(item => item.InstanceId).Intersect(bag.Items.Select(item => item.InstanceId)).Any())
            throw new InvalidDataException("背包与装备包含重复实例");
    }

    /// <summary>生成仅含公开穿戴字段的全新协议对象。</summary>
    public RoleWorldEquipmentPb ToPb()
    {
        var message = new RoleWorldEquipmentPb { RoleId = RoleId, Revision = Revision };
        message.Items.AddRange(Items.Select(item => item.ToPb()));
        return message;
    }
}
