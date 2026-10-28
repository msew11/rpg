using System;

namespace Rpg.Server.EntityCache;

/// <summary>单个角色 Actor 独占的三张实体表与瞬态模拟。</summary>
public sealed class ServerRoleCache
{
    /// <summary>本分片实体，初始化完成后始终属于同一角色。</summary>
    public RoleEntityTables? Tables { get; internal set; }
    /// <summary>待写领域：角色 1、物品 2、共同变更 3。</summary>
    internal int Dirty { get; set; }
    /// <summary>本角色的模拟、订阅和消息版本。</summary>
    public ServerSession Session { get; internal set; } = new();

    /// <summary>建立可以安全移交给地图邮箱的工作副本。</summary>
    internal ServerRoleCache Copy() => new() { Tables = Tables, Dirty = Dirty, Session = Session.Copy() };
}
