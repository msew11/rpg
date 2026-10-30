using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>保存世界同步协调器已完整验证的地图快照。</summary>
public sealed class MapCache : CacheBase
{
    /// <summary>最近接纳的完整地图范围。</summary>
    public DataMapAoiSnapshotPbData? Snapshot { get; private set; }

    /// <summary>切角清除地图引用。</summary>
    internal void Reset() => Snapshot = null;

    /// <summary>完整验证后一次替换地图。</summary>
    internal void AcceptEntry(DataMapAoiSnapshotPbData snapshot) => Snapshot = snapshot;

    /// <summary>释放地图缓存。</summary>
    protected override void OnDispose() => Reset();
}
