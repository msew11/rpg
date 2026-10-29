using System.Collections.Immutable;

namespace Rpg.ServerV2.Modules.Login.Data;

/// <summary>登录模块的不可变聚合快照，由所属 Actor 独占提交与保存。</summary>
/// <param name="Summaries">账号角色摘要表。</param>
public sealed record DataLoginSnapshot(ImmutableArray<DataRoleSummary> Summaries);
