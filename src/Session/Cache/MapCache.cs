using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>保存世界同步协调器已完整验证的地图快照。</summary>
public sealed class MapCache : CacheBase
{
    /// <summary>最近接纳的完整地图范围。</summary>
    public DataMapAoiSnapshotPbData? Snapshot { get; private set; }

    /// <summary>即时战斗覆盖层，原始快照仍供世界增量校验使用。</summary>
    private readonly System.Collections.Generic.Dictionary<System.Guid, DataMapObjectPbData> _combat = new();
    /// <summary>已合并的只读表现快照。</summary>
    internal DataMapAoiSnapshotPbData? VisibleSnapshot { get; private set; }
    /// <summary>只接纳较新的命中，死亡墓碑保留到离开当前世界。</summary>
    internal void AcceptHit(DataMapObjectPbData value)
    {
        if (_combat.TryGetValue(value.ObjectId, out var old) && old.Revision >= value.Revision) return;
        _combat[value.ObjectId] = value;
        VisibleSnapshot = Snapshot?.WithCombat(_combat);
    }

    /// <summary>切角清除地图引用。</summary>
    internal void Reset() { Snapshot = null; VisibleSnapshot = null; _combat.Clear(); }

    /// <summary>完整验证后一次替换地图。</summary>
    internal void AcceptEntry(DataMapAoiSnapshotPbData snapshot) { Snapshot = snapshot; VisibleSnapshot = _combat.Count == 0 ? snapshot : snapshot.WithCombat(_combat); }

    /// <summary>释放地图缓存。</summary>
    protected override void OnDispose() => Reset();
}
