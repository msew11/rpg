using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Rpg.Server.Entity;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Map;

namespace Rpg.Server.EntityCache;

/// <summary>无 IO 的仓库内存入口，持有实体表、摘要、会话和待发送消息。</summary>
public sealed class RepoEntityCache
{
    /// <summary>已显式加载的完整角色表。</summary>
    internal Dictionary<long, RoleEntityTables> Roles { get; } = new();
    /// <summary>登录所需摘要，包含墓碑以保证编号不复用。</summary>
    internal Dictionary<long, RoleSummaryEntity> Summaries { get; } = new();
    /// <summary>仅为删除准备的角色单例，不加载物品表。</summary>
    internal Dictionary<long, RoleEntity> Deletions { get; } = new();
    /// <summary>待落地领域：角色 1、物品 2、全部 3、未加载删除 4。</summary>
    internal Dictionary<long, int> Dirty { get; } = new();
    /// <summary>摘要已经完整加载。</summary>
    internal bool SummariesLoaded { get; set; }
    /// <summary>处理请求使用的只读配置与背包运算。</summary>
    public BagRules BagRules { get; }
    /// <summary>不可变地图规格。</summary>
    public IReadOnlyDictionary<int, MapLayout> MapLayouts { get; }
    /// <summary>新角色初始地图。</summary>
    public int InitialMapId { get; }
    /// <summary>已由外层准备的地图，不执行延迟加载。</summary>
    internal Dictionary<int, ServerMap> Maps { get; } = new();
    /// <summary>地图与角色共同落地分组。</summary>
    internal Dictionary<int, HashSet<long>> TransferGroups { get; } = new();
    /// <summary>当前请求会话。</summary>
    public ServerSession Session { get; } = new();
    /// <summary>等待外层发送的内存消息队列。</summary>
    public Queue<IMessage> Notices { get; } = new();
    /// <summary>当前选择角色的实体表，登录阶段为空。</summary>
    public RoleEntityTables? Current => Session.RoleId is { } id ? Roles.GetValueOrDefault(id) : null;

    /// <summary>建立空内存入口，不访问任何文件。</summary>
    public RepoEntityCache(BagRules? bagRules = null, IEnumerable<MapLayout>? maps = null, int initialMapId = 1)
    {
        BagRules = bagRules ?? new BagRules(8, 5, []);
        MapLayouts = new System.Collections.ObjectModel.ReadOnlyDictionary<int, MapLayout>(
            (maps ?? [new MapLayout(1, 1000, 1000, 32, 32)]).ToDictionary(map => map.MapId));
        foreach (var map in MapLayouts.Values) map.Validate();
        if (!MapLayouts.ContainsKey(initialMapId)) throw new ArgumentException("初始地图不存在");
        InitialMapId = initialMapId;
    }

    /// <summary>只读取已准备的角色，不尝试加载。</summary>
    public RoleEntityTables GetRole(long id) => Roles.TryGetValue(id, out var tables) && !tables.Role.Value.IsDeleted
        ? tables : throw new InvalidOperationException("角色尚未加载或已删除");

    /// <summary>检查当前认证与角色身份。</summary>
    public RoleEntityTables RequireRole(long id) => Session.Authenticated && Current is { } current && current.Role.Value.RoleId == id
        ? current : throw new InvalidOperationException("请先进入当前角色");

    /// <summary>角色列表操作仅允许认证且未进入角色时执行。</summary>
    public void RequireRoleList()
    {
        if (!Session.Authenticated || Current is not null) throw new InvalidOperationException("请在登录后的角色列表操作");
        if (!SummariesLoaded) throw new InvalidOperationException("角色摘要尚未加载");
    }

    /// <summary>按领域合并候选并一次发布实体、摘要和 dirty；全程仅操作内存。</summary>
    public void Commit(RoleEntityTables candidate, int domains = 3)
    {
        var id = candidate.Role.Value.RoleId;
        if (Roles.TryGetValue(id, out var previous))
        {
            if (domains == 1) candidate = previous.WithRole(candidate.Role.Value with
                { NextInstanceId = previous.Role.Value.NextInstanceId, BagExtraRows = previous.Role.Value.BagExtraRows });
            if (domains == 2) candidate = new RoleEntityTables(new RoleEntityCache(previous.Role.Value with
                { NextInstanceId = candidate.Role.Value.NextInstanceId, BagExtraRows = candidate.Role.Value.BagExtraRows }), candidate.Item, candidate.Equipment);
            if (candidate.Equals(previous)) return;
        }
        else domains = 3;
        var summary = RoleSummaryEntity.FromRole(candidate);
        Roles[id] = candidate;
        Summaries[id] = summary;
        Dirty[id] = Dirty.GetValueOrDefault(id) | domains;
    }

    /// <summary>内存删除已加载角色或外层准备的单例，不触碰未选择物品表。</summary>
    public void Delete(long id)
    {
        if (!Summaries.TryGetValue(id, out var summary) || summary.IsDeleted)
            throw new System.IO.InvalidDataException("角色不存在或已删除");
        if (Roles.TryGetValue(id, out var tables)) { Commit(tables.WithRole(tables.Role.Value with { IsDeleted = true }), 1); return; }
        if (!Deletions.TryGetValue(id, out var role)) throw new InvalidOperationException("删除角色单例尚未加载");
        Deletions[id] = role with { IsDeleted = true };
        Summaries[id] = summary with { IsDeleted = true };
        Dirty[id] = 4;
    }

    /// <summary>只获取已经准备好的地图。</summary>
    public ServerMap GetMap(int id) => Maps.TryGetValue(id, out var map) ? map : throw new InvalidOperationException("地图尚未加载");

    /// <summary>将内存转移纳入共同落地组。</summary>
    public void TrackTransfer(int mapId, long roleId)
    {
        if (!TransferGroups.TryGetValue(mapId, out var ids)) TransferGroups[mapId] = ids = new();
        ids.Add(roleId);
    }

    /// <summary>记录当前观察者可见的对象变化，不进行发送。</summary>
    public void PublishMapChange(ServerMap map, MapChange change)
    {
        if (change.Before == change.After || Current?.Role.Value.MapId != map.Rules.MapId || Session.Aoi is null) return;
        var notice = Session.Aoi.Change(map, change);
        if (notice is not null) Notices.Enqueue(notice);
    }
}
