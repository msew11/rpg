using System.Collections.Generic;
using Rpg.Server.Entity;

namespace Rpg.Server.EntityCache;

/// <summary>登录 Actor 独占的摘要索引和未落地角色管理结果。</summary>
public sealed class ServerLoginCache
{
    /// <summary>包含墓碑的摘要，编号永不复用。</summary>
    internal Dictionary<long, RoleSummaryEntity> Summaries { get; } = new();
    /// <summary>尚未被角色 Actor 接管的新建实体。</summary>
    internal Dictionary<long, RoleEntityTables> Created { get; } = new();
    /// <summary>删除所需单例，不预读物品表。</summary>
    internal Dictionary<long, RoleEntity> Deletions { get; } = new();
    /// <summary>完整摘要加载成功。</summary>
    internal bool Loaded { get; set; }
}
