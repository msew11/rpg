using System;
using System.Collections.Generic;
using Google.Protobuf;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Context;

/// <summary>角色 Handler 的独占工作区，不持有登录或地图缓存。</summary>
public sealed class RoleContext
{
    /// <summary>本次回合独占的角色实体缓存。</summary>
    public ServerRoleCache EntityCache { get; }
    /// <summary>共享只读业务规则。</summary>
    public DataServerRules Rules { get; }
    /// <summary>绑定角色工作副本，构造不执行 IO。</summary>
    public RoleContext(ServerRoleCache entityCache, DataServerRules rules)
    { EntityCache = entityCache; Rules = rules; }

    /// <summary>本次处理产生的消息，回合结束后交还发送边界。</summary>
    public Queue<IMessage> Notices { get; } = new();
    /// <summary>模拟和认证会话。</summary>
    public ServerSession Session => EntityCache.Session;
    /// <summary>本次选中的角色表。</summary>
    public RoleEntityTables? Current => Session.RoleId is { } id && EntityCache.Tables?.Role.Value.RoleId == id ? EntityCache.Tables : null;

    /// <summary>只查询已经加载的本角色实体。</summary>
    public RoleEntityTables GetRole(long id) => EntityCache.Tables is { } tables && tables.Role.Value.RoleId == id && !tables.Role.Value.IsDeleted
        ? tables : throw new InvalidOperationException("角色尚未加载或已删除");

    /// <summary>检查当前认证与角色身份。</summary>
    public RoleEntityTables RequireRole(long id) => Session.Authenticated && Current is { } current && current.Role.Value.RoleId == id
        ? current : throw new InvalidOperationException("请先进入当前角色");

    /// <summary>提交角色候选，只修改本分片并登记待写领域。</summary>
    public void Commit(RoleEntityTables candidate, int domains = 3)
    {
        var id = candidate.Role.Value.RoleId;
        var previous = EntityCache.Tables ?? throw new InvalidOperationException("角色尚未加载");
        if (previous.Role.Value.RoleId != id) throw new InvalidOperationException("角色分片身份不符");
        if (domains == 1) candidate = previous.WithRole(candidate.Role.Value with
            { NextInstanceId = previous.Role.Value.NextInstanceId, BagExtraRows = previous.Role.Value.BagExtraRows });
        if (domains == 2) candidate = new RoleEntityTables(new RoleEntityCache(previous.Role.Value with
            { NextInstanceId = candidate.Role.Value.NextInstanceId, BagExtraRows = candidate.Role.Value.BagExtraRows }), candidate.Item, candidate.Equipment);
        if (candidate.Equals(previous)) return;
        EntityCache.Tables = candidate;
        EntityCache.Dirty |= domains;
    }
}
