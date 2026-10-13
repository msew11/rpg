using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>保存世界尺寸、全部规范区块数据和当前展示状态。</summary>
[GlobalClass]
public partial class WorldChunksComponent : EcsComponent
{
    /// <summary>单个区块的地块尺寸。</summary>
    public Vector2I chunk_size { get; set; } = Vector2I.Zero;

    /// <summary>有限世界的规范区块尺寸。</summary>
    public Vector2I world_size { get; set; } = Vector2I.Zero;

    /// <summary>玩家周围需要激活的行走区块窗口尺寸。</summary>
    public Vector2I active_chunk_size { get; set; } = new(3, 3);

    /// <summary>当前需要显示的行走区块坐标。</summary>
    public Array<Vector2I> loaded { get; set; } = [];

    /// <summary>规范区块 ID 到地块数据的索引。</summary>
    private readonly System.Collections.Generic.Dictionary<Vector2I, byte[]> _tilesById = [];

    /// <summary>检查规范区块的数据是否已创建。</summary>
    public bool has_chunk(Vector2I canonicalId) => _tilesById.ContainsKey(canonicalId);

    /// <summary>返回当前持有地块数组的规范区块数量。</summary>
    public int active_count() => _tilesById.Count;

    /// <summary>返回当前持有地块数据的规范区块标识快照。</summary>
    internal Vector2I[] chunk_ids() => _tilesById.Keys.ToArray();

    /// <summary>释放指定规范区块的地块数组。</summary>
    public bool remove_chunk(Vector2I canonicalId) => _tilesById.Remove(canonicalId);

    /// <summary>读取区块地块数组的副本，避免调用方直接改动世界状态。</summary>
    public byte[] tiles_for(Vector2I canonicalId) =>
        _tilesById.TryGetValue(canonicalId, out var tiles) ? (byte[])tiles.Clone() : [];

    /// <summary>保存区块地块数组的副本，仅在内容变化时返回成功。</summary>
    public bool set_tiles(Vector2I canonicalId, byte[] tiles)
    {
        if (_tilesById.TryGetValue(canonicalId, out var existing) && existing.SequenceEqual(tiles))
            return false;
        _tilesById[canonicalId] = (byte[])tiles.Clone();
        return true;
    }
}
