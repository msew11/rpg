using System.IO;
using System.Collections.Generic;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Utils;

namespace Rpg.Session.Data;

/// <summary>仅由配置推导的公开装备展示，不包含私有实例。</summary>
/// <param name="ItemId">item.json 配置编号。</param>
/// <param name="Slot">根据配置分类推导的展示位置。</param>
public sealed record EquipmentDisplayData(int ItemId, EquipmentSlot Slot)
{
    /// <summary>在配置边界校验展示编号及可用位置。</summary>
    public static EquipmentDisplayData FromItemId(int itemId, IReadOnlyDictionary<int, ItemCfg> definitions)
    {
        if (!definitions.TryGetValue(itemId, out var cfg) || cfg.MaxStack != 1 ||
            !EquipmentSlotUtil.TryGetSlot(cfg.Category, cfg.SubCategory, out var slot))
            throw new InvalidDataException("装备展示配置不存在或不适配槽位");
        return new(itemId, slot);
    }
}
