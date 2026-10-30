using System.Collections.Immutable;
using Rpg.Server.Modules.Common.Cache;
using Rpg.Server.Modules.Role.Data;

namespace Rpg.Server.Modules.Role.Cache;

/// <summary>角色装备扩展表的独占数据值和保存基线。</summary>
public sealed class DataCacheEquipment : DataCacheArray<DataEquipment>
{
    /// <summary>用完整加载的表内容建立保存基线。</summary>
    internal DataCacheEquipment(ImmutableArray<DataEquipment> value) : base(value) { }
}
