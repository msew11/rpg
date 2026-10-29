using System.Collections.Immutable;
using System.Linq;
using Rpg.ServerV2.Modules.Common.Cache;
using Rpg.ServerV2.Modules.World.Data;

namespace Rpg.ServerV2.Modules.World.Cache;

/// <summary>地图动态对象工作区，内聚分块比较及保存基线。</summary>
public sealed class DataCacheWorldObject : DataCacheArray<DataWorldObject>
{
    /// <summary>本地图固定分块规格。</summary>
    private readonly DataWorldLayout _layout;
    /// <summary>相对保存基线变化的来源及目标块。</summary>
    public ImmutableHashSet<DataWorldChunk> DirtyChunks => Changes(Value, Saved);
    /// <summary>完整加载后建立独占工作区。</summary>
    internal DataCacheWorldObject(DataWorldLayout layout, ImmutableArray<DataWorldObject> value) : base(value) => _layout = layout;
    /// <summary>按块内顺序比较，不把跨块的数组重排视作数据变化。</summary>
    protected override bool ContentEquals(ImmutableArray<DataWorldObject> left, ImmutableArray<DataWorldObject> right) => Changes(left, right).Count == 0;
    /// <summary>按规格计算两组完整对象之间的变化块，不增加业务字段校验。</summary>
    private ImmutableHashSet<DataWorldChunk> Changes(ImmutableArray<DataWorldObject> left, ImmutableArray<DataWorldObject> right)
    {
        var current = _layout.Group(left);
        var saved = _layout.Group(right);
        return saved.Keys.Union(current.Keys).Where(chunk =>
            !saved.TryGetValue(chunk, out var before) || !current.TryGetValue(chunk, out var after) || !before.SequenceEqual(after)).ToImmutableHashSet();
    }
}
