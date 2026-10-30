using Rpg.Server.Modules.Common.Cache;

namespace Rpg.Server.Modules.Role.Cache;

/// <summary>位置和装备共享的临时公开投影状态，不落盘。</summary>
public sealed class DataCacheProjection : DataCache<DataRoleProjection>
{
    /// <summary>首次进入需要完整投影。</summary>
    internal DataCacheProjection() : base(new()) { }
    /// <summary>任一公开字段变化都递增版本并安排重试。</summary>
    internal void Changed() => Commit(Value with { Revision = checked(Value.Revision + 1), Pending = true });
    /// <summary>当前投影已被地图接纳。</summary>
    internal void Projected() => Commit(Value with { Pending = false });
}

/// <summary>不可变公开版本与待投影标记。</summary>
public sealed record DataRoleProjection(ulong Revision = 1, bool Pending = true);
