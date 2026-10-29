using System.Collections.Immutable;
using Rpg.ServerV2.Modules.Login;

namespace Rpg.ServerV2.Storage;

/// <summary>一个账号的全部角色摘要；对应 username.role_summary.json 的存储边界。</summary>
public sealed class DataAccountRoleSummaries
{
    /// <summary>与 LoginActor 分片相同的规范账号键。</summary>
    public string Username { get; }
    /// <summary>不可变摘要集合，不向其他 Actor 暴露可写表。</summary>
    public ImmutableArray<DataRoleSummary> Roles { get; }
    /// <summary>保留账号身份并冻结传入集合，默认集合按空数组处理。</summary>
    public DataAccountRoleSummaries(string username, ImmutableArray<DataRoleSummary> roles)
    { Username = UsernameKey.Normalize(username); Roles = roles.IsDefault ? [] : roles; }
}
