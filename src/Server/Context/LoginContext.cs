using System;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Context;

/// <summary>登录 Handler 的摘要工作区及当前请求认证身份。</summary>
public sealed class LoginContext
{
    /// <summary>登录 Actor 独占的摘要和待写结果。</summary>
    public ServerLoginCache EntityCache { get; }
    /// <summary>共享只读业务规则。</summary>
    public DataServerRules Rules { get; }
    /// <summary>当前连接已通过认证。</summary>
    public bool Authenticated { get; set; }
    /// <summary>当前连接已经选择角色。</summary>
    public bool HasSelectedRole { get; set; }
    /// <summary>绑定登录缓存，构造不执行 IO。</summary>
    public LoginContext(ServerLoginCache entityCache, DataServerRules rules)
    { EntityCache = entityCache; Rules = rules; }

    /// <summary>角色管理只接受已认证且未选角的请求。</summary>
    public void RequireRoleList()
    {
        if (!Authenticated || HasSelectedRole) throw new InvalidOperationException("请在登录后的角色列表操作");
        if (!EntityCache.Loaded) throw new InvalidOperationException("角色摘要尚未加载");
    }

    /// <summary>暂存创建结果并更新角色列表摘要。</summary>
    public void Create(RoleEntityTables candidate)
    {
        var id = candidate.Role.Value.RoleId;
        EntityCache.Created[id] = candidate;
        EntityCache.Summaries[id] = RoleSummaryEntity.FromRole(candidate);
    }

    /// <summary>登录列表仅变更摘要墓碑和已准备单例。</summary>
    public void Delete(long id)
    {
        if (!EntityCache.Summaries.TryGetValue(id, out var summary) || summary.IsDeleted)
            throw new System.IO.InvalidDataException("角色不存在或已删除");
        if (EntityCache.Created.TryGetValue(id, out var tables))
            EntityCache.Created[id] = tables.WithRole(tables.Role.Value with { IsDeleted = true });
        else if (EntityCache.Deletions.TryGetValue(id, out var role)) EntityCache.Deletions[id] = role with { IsDeleted = true };
        else throw new InvalidOperationException("删除角色单例尚未加载");
        EntityCache.Summaries[id] = summary with { IsDeleted = true };
    }
}
