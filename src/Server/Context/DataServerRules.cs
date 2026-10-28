using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Map;

namespace Rpg.Server.Context;

/// <summary>所有 Actor 共享的只读规则，启动时统一校验。</summary>
public sealed class DataServerRules
{
    /// <summary>背包配置及纯内存运算。</summary>
    public BagRules BagRules { get; }
    /// <summary>按地图身份索引的不可变规格。</summary>
    public IReadOnlyDictionary<int, MapLayout> MapLayouts { get; }
    /// <summary>新角色初始地图身份。</summary>
    public int InitialMapId { get; }
    /// <summary>复制配置集合并一次性校验地图。</summary>
    public DataServerRules(BagRules? bagRules = null, IEnumerable<MapLayout>? maps = null, int initialMapId = 1)
    {
        BagRules = bagRules ?? new BagRules(8, 5, []);
        MapLayouts = new System.Collections.ObjectModel.ReadOnlyDictionary<int, MapLayout>(
            (maps ?? [new MapLayout(1, 1000, 1000, 32, 32)]).ToDictionary(map => map.MapId));
        foreach (var map in MapLayouts.Values) map.Validate();
        if (!MapLayouts.ContainsKey(initialMapId)) throw new ArgumentException("初始地图不存在");
        InitialMapId = initialMapId;
    }
}
