using Rpg.Server.Map;
using Rpg.Server.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Net.Message;

namespace Rpg.Server;

/// <summary>一个观察者的范围与消息序号；不持有地图对象或文件状态。</summary>
public sealed class MapAoiTracker
{
    /// <summary>绑定的角色身份。</summary>
    private readonly long _roleId;
    /// <summary>绑定的地图身份。</summary>
    private readonly int _mapId;
    /// <summary>当前订阅的有效块集合。</summary>
    private HashSet<(int X, int Y)> _chunks = new();
    /// <summary>当前中心块。</summary>
    private (int X, int Y) _center;
    /// <summary>只在产生观察者消息时递增。</summary>
    private long _sequence;

    /// <summary>创建当前角色的地图观察者。</summary>
    public MapAoiTracker(long roleId, int mapId) { _roleId = roleId; _mapId = mapId; }

    /// <summary>复制订阅集合及序号，供跨 Actor 的独占工作副本使用。</summary>
    internal MapAoiTracker Copy() => new(_roleId, _mapId)
    { _chunks = new(_chunks), _center = _center, _sequence = _sequence };

    /// <summary>首次进入或重同步时返回当前九块，继续本观察者序号。</summary>
    public MapAoiSnapshotPb Snapshot(ServerMap map, float x, float y)
    {
        EnsureMap(map);
        _center = map.Rules.ChunkAt(x, y);
        _chunks = map.Rules.Neighborhood(_center.X, _center.Y);
        var rules = map.Rules;
        var snapshot = new MapAoiSnapshotPb
        {
            RoleId = _roleId, MapId = _mapId, Sequence = checked(++_sequence), Center = Coordinate(_center),
            Layout = new MapLayoutPb { Width = rules.Width, Height = rules.Height, TileSize = rules.TileSize, ChunkSizeTiles = rules.ChunkSizeTiles }
        };
        snapshot.Chunks.AddRange(Ordered(_chunks).Select(chunk => Chunk(map, chunk)));
        return snapshot;
    }

    /// <summary>实际位置跨块才计算差集，交集块不重发快照。</summary>
    public MapAoiNotice? Move(ServerMap map, float x, float y)
    {
        EnsureMap(map);
        var center = map.Rules.ChunkAt(x, y);
        if (center == _center) return null;
        var next = map.Rules.Neighborhood(center.X, center.Y);
        var delta = new MapAoiDeltaPb { Center = Coordinate(center) };
        delta.EnteredChunks.AddRange(Ordered(next.Except(_chunks)).Select(chunk => Chunk(map, chunk)));
        delta.LeftChunks.AddRange(Ordered(_chunks.Except(next)).Select(Coordinate));
        _center = center;
        _chunks = next;
        return Notice(delta);
    }

    /// <summary>按修改前后所属块决定新增、更新或移除；均不可见时无消息。</summary>
    public MapAoiNotice? Change(ServerMap map, MapChange change)
    {
        EnsureMap(map);
        var before = change.Before;
        var after = change.After;
        var wasVisible = before is not null && _chunks.Contains(map.Rules.ChunkAt(before.X, before.Y));
        var isVisible = after is not null && _chunks.Contains(map.Rules.ChunkAt(after.X, after.Y));
        if (!wasVisible && !isVisible) return null;
        var delta = new MapAoiDeltaPb { Center = Coordinate(_center) };
        if (isVisible) delta.UpsertObjects.Add(after!.ToPb());
        else delta.RemovedObjectIds.Add(before!.ObjectId.ToString("N"));
        return Notice(delta);
    }

    /// <summary>构造单个观察者增量信封的业务载荷。</summary>
    private MapAoiNotice Notice(MapAoiDeltaPb delta) => new()
    { RoleId = _roleId, MapId = _mapId, Sequence = checked(++_sequence), Delta = delta };

    /// <summary>复制块快照，空块也明确交付。</summary>
    private static MapChunkPb Chunk(ServerMap map, (int X, int Y) coordinate)
    {
        var value = new MapChunkPb { Coordinate = Coordinate(coordinate) };
        value.Objects.AddRange(map.InChunk(coordinate).Select(item => item.ToPb()));
        return value;
    }

    /// <summary>输出稳定顺序，便于诊断和验收。</summary>
    private static IEnumerable<(int X, int Y)> Ordered(IEnumerable<(int X, int Y)> chunks) => chunks.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X);
    /// <summary>转换为独立块坐标协议。</summary>
    private static MapChunkCoordPb Coordinate((int X, int Y) value) => new() { X = value.X, Y = value.Y };
    /// <summary>禁止将其他地图对象送给当前观察者。</summary>
    private void EnsureMap(ServerMap map)
    {
        if (map.Rules.MapId != _mapId) throw new InvalidOperationException("观察者地图身份不符");
    }
}
