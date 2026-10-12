using Godot;
using Godot.Collections;

/// <summary>向展示和存档调用方提供当前区块状态的只读视图。</summary>
[GlobalClass]
public partial class WorldChunksView : RefCounted
{
    /// <summary>只供视图读取的世界区块组件。</summary>
    private readonly WorldChunksComponent _source;

    /// <summary>供 Godot 注册只读视图类型。</summary>
    public WorldChunksView() => _source = new WorldChunksComponent();

    /// <summary>绑定管理器持有的区块组件。</summary>
    internal WorldChunksView(WorldChunksComponent source) => _source = source;

    /// <summary>读取单个区块的地块尺寸。</summary>
    public Vector2I chunk_size => _source.chunk_size;

    /// <summary>读取有限世界的规范区块尺寸。</summary>
    public Vector2I world_size => _source.world_size;

    /// <summary>读取玩家周围的活跃区块窗口尺寸。</summary>
    public Vector2I active_chunk_size => _source.active_chunk_size;

    /// <summary>返回当前加载坐标的独立列表。</summary>
    public Array<Vector2I> loaded
    {
        get
        {
            var result = new Array<Vector2I>();
            foreach (var raw in _source.loaded)
                result.Add(raw);
            return result;
        }
    }

    /// <summary>检查规范区块的数据是否已创建。</summary>
    public bool has_chunk(Vector2I canonicalId) => _source.has_chunk(canonicalId);

    /// <summary>返回当前持有地块数组的规范区块数量。</summary>
    public int active_count() => _source.active_count();

    /// <summary>返回区块地块数组的独立副本。</summary>
    public byte[] tiles_for(Vector2I canonicalId) => _source.tiles_for(canonicalId);
}
