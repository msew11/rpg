using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>一张地图唯一的可写对象状态与空间索引，仓库仅负责文件。</summary>
public sealed class ServerMap
{
    /// <summary>独立文件边界，不持有玩家或通信状态。</summary>
    private readonly RepoServerMap _repo;
    /// <summary>稳定身份到不可变对象的唯一状态表。</summary>
    private readonly Dictionary<Guid, DataServerMapObject> _objects = new();
    /// <summary>本运行期已使用的身份，删除后也不允许重用。</summary>
    private readonly HashSet<Guid> _usedIds = new();
    /// <summary>chunk 仅索引对象身份，不复制业务状态。</summary>
    private readonly Dictionary<(int X, int Y), HashSet<Guid>> _chunks = new();
    /// <summary>本轮需要落地的块。</summary>
    private readonly HashSet<(int X, int Y)> _dirty = new();
    /// <summary>复制的道具编号与堆叠上限。</summary>
    private readonly IReadOnlyDictionary<int, int> _items;
    /// <summary>不可变地图规格。</summary>
    public DataServerMapRules Rules { get; }
    /// <summary>不可写视图，不泄露内部字典修改能力。</summary>
    public IReadOnlyDictionary<Guid, DataServerMapObject> Objects { get; }

    /// <summary>完整读取并校验后建立本地图；失败不暴露半加载状态。</summary>
    public ServerMap(DataServerMapRules rules, RepoServerMap repo, IReadOnlyDictionary<int, int> items)
    {
        rules.Validate();
        Rules = rules;
        _repo = repo;
        _items = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(items));
        var loaded = repo.Load(rules);
        foreach (var value in loaded) Validate(value);
        foreach (var value in loaded) { _objects.Add(value.ObjectId, value); _usedIds.Add(value.ObjectId); Index(value); }
        Objects = new ReadOnlyDictionary<Guid, DataServerMapObject>(_objects);
    }

    /// <summary>只查询指定块的身份索引，返回独立排序快照。</summary>
    public IReadOnlyList<DataServerMapObject> InChunk((int X, int Y) chunk) =>
        _chunks.TryGetValue(chunk, out var ids) ? ids.OrderBy(id => id).Select(id => _objects[id]).ToArray() : [];

    /// <summary>创建权威对象，完整验证后才修改索引和待写集合。</summary>
    public DataServerMapChange Create(DataServerMapObject value)
    {
        Validate(value);
        if (value.Revision != 1 || _usedIds.Contains(value.ObjectId)) throw new InvalidDataException("对象 ID 重复或初始版本无效");
        _objects.Add(value.ObjectId, value);
        _usedIds.Add(value.ObjectId);
        Index(value);
        _dirty.Add(Rules.ChunkAt(value.X, value.Y));
        return new(null, value);
    }

    /// <summary>按预期版本更新同一对象，版本由服务端递增。</summary>
    public DataServerMapChange Update(DataServerMapObject value)
    {
        Validate(value);
        if (!_objects.TryGetValue(value.ObjectId, out var before) || value.Revision != before.Revision)
            throw new InvalidDataException("地图对象不存在或版本过期");
        if (value == before) return new(before, before);
        var after = value with { Revision = checked(before.Revision + 1) };
        Unindex(before);
        _objects[value.ObjectId] = after;
        Index(after);
        _dirty.Add(Rules.ChunkAt(before.X, before.Y));
        _dirty.Add(Rules.ChunkAt(after.X, after.Y));
        return new(before, after);
    }

    /// <summary>移除现存对象，空块同样保留待写标记。</summary>
    public DataServerMapChange Remove(Guid id)
    {
        if (!_objects.TryGetValue(id, out var before)) throw new InvalidDataException("地图对象不存在");
        _objects.Remove(id);
        Unindex(before);
        _dirty.Add(Rules.ChunkAt(before.X, before.Y));
        return new(before, null);
    }

    /// <summary>只在清单提交成功后清理待写标记，失败保留完整最新内存。</summary>
    public void Flush()
    {
        _repo.Commit(Rules, _dirty.ToDictionary(chunk => chunk, chunk => InChunk(chunk)));
        _dirty.Clear();
    }

    /// <summary>组合对象与地图、道具配置契约。</summary>
    private void Validate(DataServerMapObject value)
    {
        value.Validate();
        _ = Rules.ChunkAt(value.X, value.Y);
        if (value.MonsterTypeId is null && (!_items.TryGetValue(value.ItemId, out var maximum) || value.Count > maximum))
            throw new InvalidDataException("地面道具不在配置中或数量超出堆叠上限");
    }

    /// <summary>将对象身份放入所属块。</summary>
    private void Index(DataServerMapObject value)
    {
        var key = Rules.ChunkAt(value.X, value.Y);
        if (!_chunks.TryGetValue(key, out var ids)) _chunks[key] = ids = new();
        ids.Add(value.ObjectId);
    }

    /// <summary>删除旧块索引并回收空集合。</summary>
    private void Unindex(DataServerMapObject value)
    {
        var key = Rules.ChunkAt(value.X, value.Y);
        var ids = _chunks[key];
        ids.Remove(value.ObjectId);
        if (ids.Count == 0) _chunks.Remove(key);
    }
}
