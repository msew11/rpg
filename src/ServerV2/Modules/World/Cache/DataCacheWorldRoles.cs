using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Modules.Common.Cache;
using Rpg.ServerV2.Modules.World.Data;

namespace Rpg.ServerV2.Modules.World.Cache;

/// <summary>公开角色与观察状态缓存，通过块索引只标记受影响观察者。</summary>
public sealed class DataCacheWorldRoles : DataCache<ImmutableDictionary<long, DataWorldRole>>
{
    /// <summary>固定地图分块规格。</summary>
    private readonly DataWorldLayout _layout;
    /// <summary>运行时已注册的在线身份。</summary>
    private readonly HashSet<long> _online = new();
    /// <summary>每块中的角色。</summary>
    private readonly Dictionary<(int, int), HashSet<long>> _roleChunks = new();
    /// <summary>每块的观察者。</summary>
    private readonly Dictionary<(int, int), HashSet<long>> _observerChunks = new();
    /// <summary>上次发布状态。</summary>
    private readonly Dictionary<long, DataWorldObserver> _observers = new();
    /// <summary>需要发布的观察者，不扫描全体角色。</summary>
    private readonly HashSet<long> _dirty = new();
    /// <summary>初始化空的运行时投影，不从磁盘恢复在线身份。</summary>
    internal DataCacheWorldRoles(DataWorldLayout layout) : base(ImmutableDictionary<long, DataWorldRole>.Empty) => _layout = layout;
    /// <summary>计算权威位置所属块。</summary>
    internal (int X, int Y) Chunk(float x, float y) => ((int)(x / ((double)_layout.TileSize * _layout.ChunkSizeTiles)), (int)(y / ((double)_layout.TileSize * _layout.ChunkSizeTiles)));
    /// <summary>有效九块范围，边缘按规格裁剪。</summary>
    internal ImmutableHashSet<(int X, int Y)> Neighborhood((int X, int Y) center)
    {
        var result = ImmutableHashSet.CreateBuilder<(int, int)>();
        var columns = (_layout.Width - 1) / _layout.ChunkSizeTiles + 1; var rows = (_layout.Height - 1) / _layout.ChunkSizeTiles + 1;
        for (var y = Math.Max(0, center.Y - 1); y <= Math.Min(rows - 1, center.Y + 1); y++)
        for (var x = Math.Max(0, center.X - 1); x <= Math.Min(columns - 1, center.X + 1); x++) result.Add((x, y));
        return result.ToImmutable();
    }
    /// <summary>由运行时控制消息更新身份，投影消息本身不能使角色上线。</summary>
    internal void Membership(long roleId, bool online)
    {
        if (online) { _online.Add(roleId); return; }
        _online.Remove(roleId); _dirty.Remove(roleId);
        if (Value.TryGetValue(roleId, out var role))
        {
            var chunk = Chunk(role.X, role.Y); MarkObservers(chunk); Remove(_roleChunks, chunk, roleId);
            foreach (var block in Neighborhood(chunk)) Remove(_observerChunks, block, roleId);
            Commit(Value.Remove(roleId));
        }
        _observers.Remove(roleId);
    }
    /// <summary>接纳较新完整投影，更新角色与观察块索引。</summary>
    internal void Update(DataWorldRole role)
    {
        if (!_online.Contains(role.RoleId) || Value.TryGetValue(role.RoleId, out var existing) && existing.Revision >= role.Revision) return;
        var chunk = Chunk(role.X, role.Y); var first = !Value.TryGetValue(role.RoleId, out var old);
        if (!first)
        {
            var before = Chunk(old!.X, old.Y); MarkObservers(before);
            if (before != chunk)
            { Remove(_roleChunks, before, role.RoleId); foreach (var block in Neighborhood(before)) Remove(_observerChunks, block, role.RoleId); }
        }
        if (first || Chunk(old!.X, old.Y) != chunk)
        {
            Add(_roleChunks, chunk, role.RoleId);
            foreach (var block in Neighborhood(chunk)) Add(_observerChunks, block, role.RoleId);
        }
        Commit(Value.SetItem(role.RoleId, role)); MarkObservers(chunk); _dirty.Add(role.RoleId);
    }
    /// <summary>对象变化通过现有块索引标记静止观察者。</summary>
    internal void ObjectsChanged(float x, float y) => MarkObservers(Chunk(x, y));
    /// <summary>本轮是否需要发布；重同步可强制全量。</summary>
    internal bool NeedsUpdate(long roleId) => _dirty.Contains(roleId);
    /// <summary>按块收集可见角色，交集状态用于增量比较。</summary>
    internal (DataWorldRole Self, DataWorldObserver? Before, ImmutableHashSet<(int X, int Y)> Chunks, Dictionary<long, DataWorldRole> Visible) Observe(long roleId)
    {
        if (!Value.TryGetValue(roleId, out var self) || !_online.Contains(roleId)) throw new InvalidOperationException("观察角色尚未准备完成");
        var chunks = Neighborhood(Chunk(self.X, self.Y)); var visible = new Dictionary<long, DataWorldRole>();
        foreach (var chunk in chunks)
            if (_roleChunks.TryGetValue(chunk, out var roles)) foreach (var id in roles) if (id != roleId) visible.Add(id, Value[id]);
        _observers.TryGetValue(roleId, out var before); return (self, before, chunks, visible);
    }
    /// <summary>有序发布后更新观察基线。</summary>
    internal void Published(long roleId, ulong sequence, (int X, int Y) center, ImmutableHashSet<(int X, int Y)> chunks, Dictionary<long, DataWorldRole> visible, ImmutableDictionary<Guid, DataWorldObject> objects)
    {
        _observers[roleId] = new(sequence, center, chunks, visible.ToImmutableDictionary(pair => pair.Key, pair => pair.Value), objects); _dirty.Remove(roleId);
    }
    /// <summary>通过订阅索引标记受影响观察者。</summary>
    private void MarkObservers((int, int) chunk) { if (_observerChunks.TryGetValue(chunk, out var observers)) _dirty.UnionWith(observers); }
    /// <summary>添加派生索引项。</summary>
    private static void Add(Dictionary<(int, int), HashSet<long>> index, (int, int) chunk, long id)
    { if (!index.TryGetValue(chunk, out var values)) index[chunk] = values = new(); values.Add(id); }
    /// <summary>移除派生索引并回收空集合。</summary>
    private static void Remove(Dictionary<(int, int), HashSet<long>> index, (int, int) chunk, long id)
    { if (index.TryGetValue(chunk, out var values) && values.Remove(id) && values.Count == 0) index.Remove(chunk); }
}
