using System;
using Rpg.Server.EntityCache;
using Rpg.Server.Map;

namespace Rpg.Server.Context;

/// <summary>世界 Handler 的地图缓存和本次事务独占的角色工作副本。</summary>
public sealed class WorldContext
{
    /// <summary>所属 WorldActor 的唯一地图缓存。</summary>
    public ServerWorldCache EntityCache { get; }
    /// <summary>由 RoleActor 移交、完成后归还的角色上下文。</summary>
    public RoleContext Role { get; }
    /// <summary>绑定地图所有者和本次角色事务，不执行 IO。</summary>
    public WorldContext(ServerWorldCache entityCache, RoleContext role)
    { EntityCache = entityCache; Role = role; }

    /// <summary>只接受当前地图的内存实例。</summary>
    public ServerMap GetMap(int id) => EntityCache.Map is { } map && map.Rules.MapId == id
        ? map : throw new InvalidOperationException("地图尚未加载");

    /// <summary>把本角色纳入地图共同提交。</summary>
    public void TrackTransfer(int mapId, long roleId)
    {
        _ = GetMap(mapId);
        EntityCache.Transfers.Add(roleId);
    }

    /// <summary>生成当前角色可见的地图增量。</summary>
    public void PublishMapChange(ServerMap map, MapChange change)
    {
        if (change.Before == change.After || Role.Current?.Role.Value.MapId != map.Rules.MapId || Role.Session.Aoi is null) return;
        var notice = Role.Session.Aoi.Change(map, change);
        if (notice is not null) Role.Notices.Enqueue(notice);
    }
}
