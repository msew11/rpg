using System;
using System.Collections.Immutable;
using Rpg.Server.Modules.World.Data;

namespace Rpg.Server.Modules.World.Cache;

/// <summary>地图邮箱独占工作区，组织固定规格与动态对象缓存。</summary>
public sealed class WorldCache
{
    /// <summary>固定地图身份。</summary>
    public int MapId => Layout.MapId;
    /// <summary>从宿主配置复制的不可变规格。</summary>
    public DataWorldLayout Layout { get; }
    /// <summary>当前在线角色及观察范围的临时公开缓存。</summary>
    public DataCacheWorldRoles Roles { get; }
    /// <summary>全部动态对象及其分块保存状态。</summary>
    public DataCacheWorldObject Objects { get; }
    /// <summary>汇总对象缓存的变化块。</summary>
    public ImmutableHashSet<DataWorldChunk> DirtyChunks => Objects.DirtyChunks;
    /// <summary>存储完整加载成功后建立工作区。</summary>
    internal WorldCache(DataWorldLayout layout, DataWorldSnapshot snapshot) { Layout = layout; Objects = new(layout, snapshot.Objects); Roles = new(layout); }
    /// <summary>导出不可变对象数组供存储提交。</summary>
    public DataWorldSnapshot Snapshot() => new(Objects.Value);
    /// <summary>一次替换完整地图对象，不新增业务校验。</summary>
    public void Commit(DataWorldSnapshot snapshot) { ArgumentNullException.ThrowIfNull(snapshot); Objects.Commit(snapshot.Objects); }
    /// <summary>清单成功提交后更新对象保存基线。</summary>
    internal void AcceptSaved() => Objects.AcceptSaved();
}
