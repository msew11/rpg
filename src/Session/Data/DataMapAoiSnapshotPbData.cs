using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Protocol;

namespace Rpg.Session.Data;

/// <summary>当前地图 AOI 的唯一不可变客户端快照，原子替换而非部分写入。</summary>
public sealed class DataMapAoiSnapshotPbData
{
    /// <summary>当前地图身份。</summary>
    public int MapId { get; }
    /// <summary>当前观察者角色。</summary>
    public long RoleId { get; }
    /// <summary>最后接纳的消息序号。</summary>
    public long Sequence { get; }
    /// <summary>独立地图规格。</summary>
    public DataMapLayoutPbData Layout { get; }
    /// <summary>权威位置的中心块。</summary>
    public (int X, int Y) Center { get; }
    /// <summary>包含空块在内的完整有效订阅集合。</summary>
    public FrozenSet<(int X, int Y)> Chunks { get; }
    /// <summary>稳定身份索引的不可变对象集合。</summary>
    public FrozenDictionary<Guid, DataMapObjectPbData> Objects { get; }

    /// <summary>私有构造只接受已完成验证的独立状态。</summary>
    private DataMapAoiSnapshotPbData(int mapId, long roleId, long sequence, DataMapLayoutPbData layout,
        (int X, int Y) center, IEnumerable<(int X, int Y)> chunks, Dictionary<Guid, DataMapObjectPbData> objects)
    {
        MapId = mapId; RoleId = roleId; Sequence = sequence; Layout = layout; Center = center;
        Chunks = chunks.ToFrozenSet(); Objects = objects.ToFrozenDictionary();
    }

    /// <summary>直接接纳 初始范围，内部序号仅供现有只读视图使用。</summary>
    public static DataMapAoiSnapshotPbData FromPb(global::Rpg.Protocol.MapAoiSnapshotPb message, long roleId)
    {
        if (message is null || message.MapId <= 0 || roleId <= 0) throw new InvalidDataException("地图快照身份或序号无效");
        var layout = DataMapLayoutPbData.FromPb(message.Layout);
        var center = Coordinate(message.Center);
        var expected = layout.Neighborhood(center);
        var seen = new HashSet<(int, int)>();
        var objects = new Dictionary<Guid, DataMapObjectPbData>();
        foreach (var chunk in message.Chunks) ReadChunk(chunk, layout, expected, seen, objects);
        if (!seen.SetEquals(expected)) throw new InvalidDataException("地图快照缺少有效订阅块");
        return new(message.MapId, roleId, 1, layout, center, expected, objects);
    }

    /// <summary>完整校验订阅差集和交集对象变化，再构造独立候选。</summary>
    public DataMapAoiSnapshotPbData Apply(global::Rpg.Protocol.MapAoiDeltaPb delta, long sequence)
    {
        var center = Coordinate(delta.Center);
        var nextChunks = Layout.Neighborhood(center);
        var entered = nextChunks.Except(Chunks).ToHashSet();
        var left = Chunks.Except(nextChunks).ToHashSet();
        var suppliedLeft = delta.LeftChunks.Select(Coordinate).ToArray();
        if (suppliedLeft.Distinct().Count() != suppliedLeft.Length || !left.SetEquals(suppliedLeft))
            throw new InvalidDataException("离开块不匹配订阅差集");
        var objects = Objects.Values.Where(value => !left.Contains(Layout.ChunkAt(value.X, value.Y))).ToDictionary(value => value.ObjectId);
        var seen = new HashSet<(int, int)>();
        var enteredObjects = new Dictionary<Guid, DataMapObjectPbData>();
        foreach (var chunk in delta.EnteredChunks) ReadChunk(chunk, Layout, entered, seen, enteredObjects);
        if (!seen.SetEquals(entered)) throw new InvalidDataException("进入块不匹配订阅差集");
        var touched = new HashSet<Guid>();
        foreach (var idText in delta.RemovedObjectIds)
        {
            if (!Guid.TryParseExact(idText, "N", out var id) || !touched.Add(id) || !objects.Remove(id))
                throw new InvalidDataException("移除地图对象身份无效或重复");
        }
        foreach (var item in delta.UpsertedObjects)
        {
            var value = DataMapObjectPbData.FromPb(item);
            var chunk = Layout.ChunkAt(value.X, value.Y);
            if (!nextChunks.Contains(chunk) || entered.Contains(chunk) || !touched.Add(value.ObjectId) ||
                (Objects.TryGetValue(value.ObjectId, out var old) && value.Revision <= old.Revision))
                throw new InvalidDataException("地图对象增量位置或版本无效");
            objects[value.ObjectId] = value;
        }
        foreach (var (id, value) in enteredObjects)
            if (touched.Contains(id) || !objects.TryAdd(id, value)) throw new InvalidDataException("块快照与增量对象重复");
        return new(MapId, RoleId, sequence, Layout, center, nextChunks, objects);
    }

    /// <summary>检查 块与对象归属及唯一性。</summary>
    private static void ReadChunk(global::Rpg.Protocol.MapChunkPb chunk, DataMapLayoutPbData layout, HashSet<(int X, int Y)> expected,
        HashSet<(int X, int Y)> seen, Dictionary<Guid, DataMapObjectPbData> objects)
    {
        var coordinate = Coordinate(chunk.Coordinate);
        if (!expected.Contains(coordinate) || !seen.Add(coordinate)) throw new InvalidDataException("地图块越界或重复");
        foreach (var item in chunk.Objects)
        {
            var value = DataMapObjectPbData.FromPb(item);
            if (layout.ChunkAt(value.X, value.Y) != coordinate || !objects.TryAdd(value.ObjectId, value))
                throw new InvalidDataException("地图对象归属错误或重复");
        }
    }

    /// <summary>缺失的坐标不隐式解释为原点。</summary>
    private static (int X, int Y) Coordinate(global::Rpg.Protocol.MapChunkCoordPb? value) => value is null
        ? throw new InvalidDataException("缺少地图块坐标") : (value.X, value.Y);
}
