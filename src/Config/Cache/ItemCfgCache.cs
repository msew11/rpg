using System;
using System.Collections.Generic;
using System.IO;
using Rpg.Config.Data;
using Rpg.Const;

namespace Rpg.Config.Cache;

/// <summary>加载道具配置数组，并按 ItemCfg 标注的主键索引。</summary>
[CfgFile("item.json")]
public sealed class ItemCfgCache : MultiCfgCache<int, ItemCfg>
{
    /// <summary>每个大类包含全部同类记录，保留配置顺序。</summary>
    private readonly Dictionary<ItemCategory, List<ItemCfg>> _byCategory = new();
    /// <summary>大小类组合的双键索引，同类记录不互相覆盖。</summary>
    private readonly Dictionary<(ItemCategory, ItemSubCategory), List<ItemCfg>> _byCategoryAndSubCategory = new();

    /// <summary>由配置服务创建并拥有道具缓存。</summary>
    internal ItemCfgCache() { }

    /// <summary>拒绝枚举未定义值及不匹配的大小类，避免数字解码静默通过。</summary>
    protected override void Validate(ItemCfg value)
    {
        if (!Enum.IsDefined(value.Category))
            throw new InvalidDataException($"道具 id={value.Id} 的 category={(int)value.Category} 不属于 ItemCategory 枚举");
        if (!Enum.IsDefined(value.SubCategory))
            throw new InvalidDataException($"道具 id={value.Id} 的 subCategory={(int)value.SubCategory} 不属于 ItemSubCategory 枚举");
        var matches = value.Category switch
        {
            ItemCategory.Weapon => value.SubCategory is ItemSubCategory.Spear or ItemSubCategory.Sword or
                ItemSubCategory.TwoHandedWeapon or ItemSubCategory.Katar or ItemSubCategory.Staff or ItemSubCategory.ThrowingWeapon,
            ItemCategory.Armor => value.SubCategory == ItemSubCategory.Shield,
            _ => false
        };
        if (!matches)
            throw new InvalidDataException($"道具 id={value.Id} 的 category={value.Category} 与 subCategory={value.SubCategory} 不匹配");
    }

    /// <summary>整表就绪后一次性构建两种分组，索引值始终为列表。</summary>
    protected override void BuildIndexes()
    {
        foreach (var item in Items.Values)
        {
            if (!_byCategory.TryGetValue(item.Category, out var categoryItems))
                _byCategory[item.Category] = categoryItems = new List<ItemCfg>();
            categoryItems.Add(item);
            var key = (item.Category, item.SubCategory);
            if (!_byCategoryAndSubCategory.TryGetValue(key, out var typedItems))
                _byCategoryAndSubCategory[key] = typedItems = new List<ItemCfg>();
            typedItems.Add(item);
        }
    }

    /// <summary>按大类返回列表副本，未命中返回空列表，不暴露缓存中的可变列表。</summary>
    public List<ItemCfg> GetByCategory(ItemCategory category)
    {
        _ = Items;
        return _byCategory.TryGetValue(category, out var items) ? new List<ItemCfg>(items) : new List<ItemCfg>();
    }

    /// <summary>按大小类双键返回列表副本，未命中返回空列表。</summary>
    public List<ItemCfg> GetByCategoryAndSubCategory(ItemCategory category, ItemSubCategory subCategory)
    {
        _ = Items;
        return _byCategoryAndSubCategory.TryGetValue((category, subCategory), out var items)
            ? new List<ItemCfg>(items) : new List<ItemCfg>();
    }

    /// <summary>随主键映射一起清理两个分类索引。</summary>
    protected override void ClearIndexes()
    {
        _byCategory.Clear();
        _byCategoryAndSubCategory.Clear();
    }
}
