using System.Collections.Immutable;

namespace Rpg.ServerV2.Modules.World.Data;

/// <summary>仓库与地图邮箱交换的完整不可变对象数组。</summary>
/// <param name="Objects">全部动态对象；默认数组在使用入口按空数组处理。</param>
public sealed record DataWorldSnapshot(ImmutableArray<DataWorldObject> Objects);
