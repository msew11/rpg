using System;
using Rpg.Server.Modules.Login.Data;

namespace Rpg.Server.Modules.Login.Cache;

/// <summary>账号邮箱独占的可变工作区，组织摘要缓存和聚合导出。</summary>
public sealed class LoginCache
{
    /// <summary>规范账号身份，不包含连接认证状态。</summary>
    public string Username { get; }
    /// <summary>账号摘要及墓碑的数据缓存。</summary>
    public DataCacheRoleSummary Summaries { get; }
    /// <summary>摘要是否相对成功保存基线变化。</summary>
    public bool SummariesDirty => Summaries.IsDirty;
    /// <summary>完整加载成功后建立账号工作区。</summary>
    internal LoginCache(string username, DataLoginSnapshot snapshot) { Username = username; Summaries = new(snapshot.Summaries); }
    /// <summary>导出与后续工作区修改隔离的不可变数据。</summary>
    public DataLoginSnapshot Snapshot() => new(Summaries.Value);
    /// <summary>一次接纳完整账号数据，默认数组按空数组处理。</summary>
    public void Commit(DataLoginSnapshot snapshot) { ArgumentNullException.ThrowIfNull(snapshot); Summaries.Commit(snapshot.Summaries); }
    /// <summary>存储保存成功后接受所有数据缓存的基线。</summary>
    internal void AcceptSaved() => Summaries.AcceptSaved();
}
