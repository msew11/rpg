using System.Collections.Immutable;

namespace Rpg.ServerV2.Modules.Login.Data;

/// <summary>登录缓存导出的不可变传输值，供聚合提交及仓库保存使用。</summary>
/// <param name="Summaries">账号角色摘要表。</param>
public sealed record DataLoginSnapshot(ImmutableArray<DataRoleSummary> Summaries);
