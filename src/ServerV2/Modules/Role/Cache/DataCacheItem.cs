using System.Collections.Immutable;
using Rpg.ServerV2.Modules.Common.Cache;
using Rpg.ServerV2.Modules.Role.Data;

namespace Rpg.ServerV2.Modules.Role.Cache;

/// <summary>角色物品表的独占数据值和保存基线。</summary>
public sealed class DataCacheItem : DataCacheArray<DataItem>
{
    /// <summary>用完整加载的表内容建立保存基线。</summary>
    internal DataCacheItem(ImmutableArray<DataItem> value) : base(value) { }
}
