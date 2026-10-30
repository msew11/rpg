using System.Collections.Immutable;

namespace Rpg.Server.Modules.World.Data;

/// <summary>地图缓存导出的完整不可变传输值，不承担运行时状态管理。</summary>
/// <param name="Objects">全部动态对象；默认数组在使用入口按空数组处理。</param>
public sealed record DataWorldSnapshot(ImmutableArray<DataWorldObject> Objects);
