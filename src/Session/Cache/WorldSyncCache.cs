using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Frozen;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>世界帧唯一接纳入口，验证整批后同步提交三个领域。</summary>
internal sealed class WorldSyncCache : CacheBase
{
    /// <summary>本人权威缓存。</summary>
    private readonly RoleCache _role;
    /// <summary>地图权威缓存。</summary>
    private readonly MapCache _map;
    /// <summary>最后成功序号。</summary>
    internal ulong Sequence { get; private set; }
    /// <summary>对应服务器时间。</summary>
    internal long ServerTimeMs { get; private set; }
    /// <summary>不可变其他玩家集合。</summary>
    internal IReadOnlyDictionary<long, DataPublicRole> Roles { get; private set; } = FrozenDictionary<long, DataPublicRole>.Empty;
    /// <summary>缺口后暂停增量。</summary>
    internal bool NeedsResync { get; private set; }
    /// <summary>重同步请求唯一在途标记。</summary>
    internal bool ResyncPending { get; set; }
    /// <summary>缓存阶段监听与本人缓存同时建立。</summary>
    internal WorldSyncCache(RoleCache role, MapCache map)
    {
        _role = role; _map = map;
        Subscribe<SessionKickedNotice>(_ => { _role.Reset(); _map.Reset(); Reset(); });
        Subscribe<WorldUpdateNotice>(notice => Accept(notice.Frame));
        Subscribe<ResyncWorldRt>(reply => { ResyncPending = false; if (reply.Frame?.Snapshot is null) throw new InvalidDataException("重同步必须返回全量"); Accept(reply.Frame); });
        Subscribe<ErrorRt>(_ => ResyncPending = false);
    }
    /// <summary>切角后等待第一份全量，不将代次写进业务协议。</summary>
    internal void Reset() { Sequence = 0; ServerTimeMs = 0; Roles = FrozenDictionary<long, DataPublicRole>.Empty; NeedsResync = false; ResyncPending = false; }
    /// <summary>任何候选转换失败都不提交其他领域。</summary>
    internal void Accept(WorldFramePb frame)
    {
        if (!_role.IsInitialized) return;
        if (frame is null || frame.Sequence == 0) throw new InvalidDataException("世界帧缺少序号");
        if (frame.Sequence <= Sequence) return;
        if (frame.Snapshot is null && (NeedsResync || frame.Sequence != Sequence + 1)) { NeedsResync = true; return; }
        if (frame.ServerTimeMs < ServerTimeMs || frame.AcknowledgedAction > long.MaxValue || frame.Self?.RoleId != _role.RoleId)
            throw new InvalidDataException("世界帧身份、时间或确认无效");
        var position = RoleWorldPositionPbData.FromPb(frame.Self.Position, _role.RoleId) with
            { Revision = checked((long)frame.Sequence), AcknowledgedAction = (long)frame.AcknowledgedAction };
        if (position.AcknowledgedAction < _role.State.AcknowledgedAction) throw new InvalidDataException("动作确认回退");
        var oldMap = _map.Snapshot ?? throw new InvalidDataException("世界帧早于进入响应");
        var map = frame.Snapshot is not null ? DataMapAoiSnapshotPbData.FromPb(frame.Snapshot.Map, _role.RoleId) :
            frame.Delta?.Map is { } delta ? oldMap.Apply(delta, checked((long)frame.Sequence)) : oldMap;
        if (map.MapId != _role.State.MapId || map.Layout != oldMap.Layout || map.Layout.ChunkAt(position.Position.X, position.Position.Y) != map.Center)
            throw new InvalidDataException("世界位置与地图范围不一致");
        ValidatePosition(position, map);
        var roles = frame.Snapshot is not null ? new Dictionary<long, DataPublicRole>() : Roles.ToDictionary(pair => pair.Key, pair => pair.Value);
        var touched = new HashSet<long>();
        foreach (var value in frame.Snapshot?.Roles ?? frame.Delta?.EnteredRoles ?? throw new InvalidDataException("世界帧缺少内容"))
        {
            var role = DataPublicRole.FromPb(value, GameServices.Config.Item.Items);
            if (role.RoleId == _role.RoleId || !touched.Add(role.RoleId) || !roles.TryAdd(role.RoleId, role)) throw new InvalidDataException("进入角色重复");
        }
        if (frame.Delta is { } changes)
        {
            foreach (var id in changes.LeftRoleIds)
                if (!touched.Add(id) || !roles.Remove(id)) throw new InvalidDataException("离开角色无效或重复");
            var exclusive = new HashSet<long>(touched);
            foreach (var value in changes.MovedRoles)
            {
                if (!touched.Add(value.RoleId) || !roles.TryGetValue(value.RoleId, out var role)) throw new InvalidDataException("移动角色无效或重复");
                roles[value.RoleId] = role with { Position = RoleWorldPositionPbData.FromPb(value.Position, value.RoleId) };
            }
            var equipped = new HashSet<long>();
            foreach (var value in changes.EquippedRoles)
            {
                if (exclusive.Contains(value.RoleId) || !equipped.Add(value.RoleId) || !roles.TryGetValue(value.RoleId, out var role)) throw new InvalidDataException("装备角色无效或重复");
                roles[value.RoleId] = role with { Equipment = RoleWorldEquipmentPbData.FromPb(value.Equipment, value.RoleId, GameServices.Config.Item.Items) };
            }
        }
        foreach (var role in roles.Values)
        {
            ValidatePosition(role.Position, map);
            if (!map.Chunks.Contains(map.Layout.ChunkAt(role.Position.Position.X, role.Position.Position.Y))) throw new InvalidDataException("角色超出观察范围");
        }
        foreach (var item in map.Objects.Values)
            if (item.MonsterTypeId is null && (!GameServices.Config.Item.Items.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack)) throw new InvalidDataException("地图物品无效");
        var prepared = roles.ToFrozenDictionary();
        _role.AcceptPosition(position); _map.AcceptEntry(map); Roles = prepared;
        Sequence = frame.Sequence; ServerTimeMs = frame.ServerTimeMs; NeedsResync = false; ResyncPending = false;
    }
    /// <summary>坐标与目标使用同一地图合法范围。</summary>
    private static void ValidatePosition(RoleWorldPositionPbData position, DataMapAoiSnapshotPbData map)
    {
        if (position.MapId != map.MapId) throw new InvalidDataException("公开位置地图不符");
        _ = map.Layout.ChunkAt(position.Position.X, position.Position.Y);
        if (position.Target is { } target) _ = map.Layout.ChunkAt(target.X, target.Y);
    }
    /// <summary>释放世界集合。</summary>
    protected override void OnDispose() => Reset();
}
