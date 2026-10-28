using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Rpg.Server.Entity;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Map;

namespace Rpg.Server.EntityCache;

/// <summary>单次 Handler 的纯内存工作区，不负责全局缓存或加载。</summary>
public sealed class ServerRequestContext
{
    /// <summary>登录处理时的摘要工作区。</summary>
    public ServerLoginCache Login { get; }
    /// <summary>本次处理独占的角色工作区。</summary>
    public ServerRoleCache Role { get; }
    /// <summary>仅地图 Actor 回合可以绑定的地图。</summary>
    public ServerWorldCache? World { get; }
    /// <summary>只读背包配置与运算。</summary>
    public BagRules BagRules { get; }
    /// <summary>不可变地图规格。</summary>
    public IReadOnlyDictionary<int, MapLayout> MapLayouts { get; }
    /// <summary>新角色初始地图。</summary>
    public int InitialMapId { get; }
    /// <summary>本次处理产生的消息，回合结束后交还发送边界。</summary>
    public Queue<IMessage> Notices { get; } = new();
    /// <summary>模拟和认证会话。</summary>
    public ServerSession Session => Role.Session;
    /// <summary>本次选中的角色表。</summary>
    public RoleEntityTables? Current => Session.RoleId is { } id && Role.Tables?.Role.Value.RoleId == id ? Role.Tables : null;

    /// <summary>绑定本次独占状态与只读规则，构造不执行 IO。</summary>
    public ServerRequestContext(BagRules? bagRules = null, IEnumerable<MapLayout>? maps = null, int initialMapId = 1,
        ServerLoginCache? login = null, ServerRoleCache? role = null, ServerWorldCache? world = null)
    {
        BagRules = bagRules ?? new BagRules(8, 5, []);
        MapLayouts = new System.Collections.ObjectModel.ReadOnlyDictionary<int, MapLayout>(
            (maps ?? [new MapLayout(1, 1000, 1000, 32, 32)]).ToDictionary(map => map.MapId));
        foreach (var map in MapLayouts.Values) map.Validate();
        if (!MapLayouts.ContainsKey(initialMapId)) throw new ArgumentException("初始地图不存在");
        InitialMapId = initialMapId;
        Login = login ?? new();
        Role = role ?? new();
        World = world;
    }

    /// <summary>只查询已经加载的本角色实体。</summary>
    public RoleEntityTables GetRole(long id) => Role.Tables is { } tables && tables.Role.Value.RoleId == id && !tables.Role.Value.IsDeleted
        ? tables : throw new InvalidOperationException("角色尚未加载或已删除");

    /// <summary>检查当前认证与角色身份。</summary>
    public RoleEntityTables RequireRole(long id) => Session.Authenticated && Current is { } current && current.Role.Value.RoleId == id
        ? current : throw new InvalidOperationException("请先进入当前角色");

    /// <summary>角色管理只接受已认证且未选角的请求。</summary>
    public void RequireRoleList()
    {
        if (!Session.Authenticated || Current is not null) throw new InvalidOperationException("请在登录后的角色列表操作");
        if (!Login.Loaded) throw new InvalidOperationException("角色摘要尚未加载");
    }

    /// <summary>提交角色候选；登录创建只暂存，已加载角色只修改本分片。</summary>
    public void Commit(RoleEntityTables candidate, int domains = 3)
    {
        var id = candidate.Role.Value.RoleId;
        if (Role.Tables is not { } previous)
        {
            Login.Created[id] = candidate;
            Login.Summaries[id] = RoleSummaryEntity.FromRole(candidate);
            return;
        }
        if (previous.Role.Value.RoleId != id) throw new InvalidOperationException("角色分片身份不符");
        if (domains == 1) candidate = previous.WithRole(candidate.Role.Value with
            { NextInstanceId = previous.Role.Value.NextInstanceId, BagExtraRows = previous.Role.Value.BagExtraRows });
        if (domains == 2) candidate = new RoleEntityTables(new RoleEntityCache(previous.Role.Value with
            { NextInstanceId = candidate.Role.Value.NextInstanceId, BagExtraRows = candidate.Role.Value.BagExtraRows }), candidate.Item, candidate.Equipment);
        if (candidate.Equals(previous)) return;
        Role.Tables = candidate;
        Role.Dirty |= domains;
    }

    /// <summary>登录列表仅变更摘要墓碑和已准备单例。</summary>
    public void Delete(long id)
    {
        if (!Login.Summaries.TryGetValue(id, out var summary) || summary.IsDeleted)
            throw new System.IO.InvalidDataException("角色不存在或已删除");
        if (Login.Created.TryGetValue(id, out var tables))
            Login.Created[id] = tables.WithRole(tables.Role.Value with { IsDeleted = true });
        else if (Login.Deletions.TryGetValue(id, out var role)) Login.Deletions[id] = role with { IsDeleted = true };
        else throw new InvalidOperationException("删除角色单例尚未加载");
        Login.Summaries[id] = summary with { IsDeleted = true };
    }

    /// <summary>只接受当前地图的内存实例。</summary>
    public ServerMap GetMap(int id) => World?.Map is { } map && map.Rules.MapId == id
        ? map : throw new InvalidOperationException("地图尚未加载");

    /// <summary>把本角色纳入地图共同提交。</summary>
    public void TrackTransfer(int mapId, long roleId)
    {
        _ = GetMap(mapId);
        World!.Transfers.Add(roleId);
    }

    /// <summary>生成当前角色可见的地图增量。</summary>
    public void PublishMapChange(ServerMap map, MapChange change)
    {
        if (change.Before == change.After || Current?.Role.Value.MapId != map.Rules.MapId || Session.Aoi is null) return;
        var notice = Session.Aoi.Change(map, change);
        if (notice is not null) Notices.Enqueue(notice);
    }
}
