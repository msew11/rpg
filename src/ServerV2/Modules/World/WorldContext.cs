using System;
using System.Collections.Immutable;
using System.Linq;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.World.Data;

namespace Rpg.ServerV2.Modules.World;

/// <summary>地图邮箱独占的规格、完整动态对象和保存基线。</summary>
public sealed class WorldContext : ModuleContext
{
    /// <summary>本分片固定地图身份。</summary>
    public int MapId => Layout.MapId;
    /// <summary>INIT 从宿主配置复制的固定规格。</summary>
    public DataWorldLayout Layout { get; }
    /// <summary>当前完整不可变对象数组，仅通过 Commit 替换。</summary>
    public ImmutableArray<DataWorldObject> Objects { get; private set; }
    /// <summary>相对最近成功保存基线变化的块。</summary>
    public ImmutableHashSet<DataWorldChunk> DirtyChunks { get; private set; } = ImmutableHashSet<DataWorldChunk>.Empty;
    /// <summary>最近完整加载或成功提交的快照。</summary>
    private DataWorldSnapshot _saved;
    /// <summary>只有仓库完整加载成功后才创建工作区。</summary>
    internal WorldContext(ConfigService config, DataWorldLayout layout, DataWorldSnapshot snapshot) : base(config)
    {
        Layout = layout;
        Objects = snapshot.Objects.IsDefault ? [] : snapshot.Objects;
        _saved = Snapshot();
    }
    /// <summary>返回本回合当前不可变快照。</summary>
    public DataWorldSnapshot Snapshot() => new(Objects);
    /// <summary>按内容比较来源及目标块；恢复基线自动取消待写，不验证对象业务字段。</summary>
    public void Commit(DataWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var objects = snapshot.Objects.IsDefault ? ImmutableArray<DataWorldObject>.Empty : snapshot.Objects;
        var saved = Layout.Group(_saved.Objects);
        var current = Layout.Group(objects);
        var dirty = saved.Keys.Union(current.Keys).Where(chunk =>
            !saved.TryGetValue(chunk, out var before) || !current.TryGetValue(chunk, out var after) || !before.SequenceEqual(after));
        var changes = dirty.ToImmutableHashSet();
        Objects = objects;
        DirtyChunks = changes;
    }
    /// <summary>仅在清单成功提交后更新基线。</summary>
    internal void AcceptSaved() { _saved = Snapshot(); DirtyChunks = ImmutableHashSet<DataWorldChunk>.Empty; }
}
