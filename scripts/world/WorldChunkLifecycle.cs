using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>管理活跃规范区块实体及其临时存档生命周期。</summary>
internal sealed class WorldChunkLifecycle
{
    /// <summary>区块实体所属的 ECS 世界。</summary>
    private readonly EcsWorld _ecs;

    /// <summary>区块地块与实体树使用的会话缓存。</summary>
    private readonly LocalDataRepoCache _cache;

    /// <summary>世界实体标识。</summary>
    private readonly long _worldEntityId;

    /// <summary>保存加载范围与活跃地块的组件。</summary>
    private readonly WorldChunksComponent _chunks;

    /// <summary>临时落地失败时由世界边界发出错误通知。</summary>
    private readonly Action<string> _reportError;

    /// <summary>不可变测试地图配置，用于生成区块基准地块。</summary>
    private GodotObject? _cfg;

    /// <summary>活跃规范区块与其实体标识。</summary>
    private readonly Dictionary<Vector2I, long> _chunkEntities = [];

    /// <summary>绑定区块生命周期依赖。</summary>
    internal WorldChunkLifecycle(EcsWorld ecs, LocalDataRepoCache cache, long worldEntityId,
        WorldChunksComponent chunks, Action<string> reportError)
    {
        _ecs = ecs;
        _cache = cache;
        _worldEntityId = worldEntityId;
        _chunks = chunks;
        _reportError = reportError;
    }

    /// <summary>绑定会话测试地图配置。</summary>
    internal void SetConfig(GodotObject cfg) => _cfg = cfg;

    /// <summary>在加载范围变化后卸载离开区块并创建新进入的区块。</summary>
    internal void Sync(Vector2I[] previousLoaded)
    {
        if (_cfg is null)
            return;
        var current = _chunks.loaded;
        foreach (var raw in previousLoaded)
        {
            if (current.Contains(raw))
                continue;
            var canonical = WorldMath.CanonicalChunk(raw, _chunks.world_size);
            if (!current.Any(candidate => WorldMath.CanonicalChunk(candidate, _chunks.world_size) == canonical))
                Unload(raw);
        }
        foreach (var raw in current)
        {
            var canonical = WorldMath.CanonicalChunk(raw, _chunks.world_size);
            if (_chunkEntities.ContainsKey(canonical))
                continue;
            if (!_chunks.has_chunk(canonical))
            {
                var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
                _chunks.set_tiles(canonical, _cache.load_chunk(canonical, baseline));
            }
            var entityId = _ecs.CreateEntity(_worldEntityId);
            _ecs.AddComponent(entityId, new ChunkIdComponent(canonical));
            _chunkEntities.Add(canonical, entityId);
            WorldEntityTreeCodec.Restore(_ecs, entityId, _cache.load_entities_json(canonical));
        }
    }

    /// <summary>落地一个不再活跃的区块及其子实体，然后释放内存；失败仍卸载。</summary>
    private bool Unload(Vector2I raw)
    {
        if (_cfg is null)
            return false;
        var canonical = WorldMath.CanonicalChunk(raw, _chunks.world_size);
        if (_chunks.loaded.Any(candidate => WorldMath.CanonicalChunk(candidate, _chunks.world_size) == canonical))
            return false;
        if (!_chunkEntities.Remove(canonical, out var entityId))
            return false;
        var tiles = _chunks.tiles_for(canonical);
        var baseline = _cfg.Call("tiles_for", canonical).AsByteArray();
        var persisted = _cache.unload_chunk(canonical, tiles, baseline, WorldEntityTreeCodec.Capture(_ecs, entityId));
        if (!persisted)
            _reportError($"区块 {canonical.X},{canonical.Y} 临时存档写入失败，本次改动可能丢失。");
        _chunks.remove_chunk(canonical);
        _ecs.DestroyEntity(entityId);
        return persisted;
    }

    /// <summary>读取规范区块的活跃实体标识，未激活时返回零。</summary>
    internal long ChunkEntity(Vector2I raw) =>
        _chunkEntities.GetValueOrDefault(WorldMath.CanonicalChunk(raw, _chunks.world_size));

    /// <summary>收集活跃规范区块实体及其子树的落地内容。</summary>
    internal string Capture(Vector2I raw)
    {
        var entityId = ChunkEntity(raw);
        return entityId == 0 ? "{}" : WorldEntityTreeCodec.Capture(_ecs, entityId);
    }

    /// <summary>逐一读取活跃规范区块的实体树，供变化通知比较。</summary>
    internal Dictionary<Vector2I, string> CaptureActiveEntities()
    {
        var result = new Dictionary<Vector2I, string>();
        foreach (var (canonical, entityId) in _chunkEntities)
            result[canonical] = WorldEntityTreeCodec.Capture(_ecs, entityId);
        return result;
    }
}
