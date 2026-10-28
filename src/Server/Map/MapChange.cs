using Rpg.Server.Entity;

namespace Rpg.Server.Map;

/// <summary>一次已提交内存修改的前后状态，供观察者计算可见性。</summary>
/// <param name="Before">修改前状态，新增时为空。</param>
/// <param name="After">修改后状态，删除时为空。</param>
public sealed record MapChange(MapObjectEntity? Before, MapObjectEntity? After);
