using Rpg.ServerV2.Storage;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>单个账号 Actor 的独占内存上下文，不包含连接认证状态。</summary>
public sealed class LoginContext
{
    /// <summary>当前分片的规范账号身份。</summary>
    public string Username { get; }
    /// <summary>本账号持有的不可变摘要快照。</summary>
    public DataAccountRoleSummaries Summaries { get; private set; }
    /// <summary>建立空账号工作区，不推断账号是否存在、不执行 IO。</summary>
    internal LoginContext(string username)
    { Username = username; Summaries = new(username, []); }
    /// <summary>仅在所属回合接纳同账号的新快照。</summary>
    public void ReplaceSummaries(DataAccountRoleSummaries summaries)
    {
        System.ArgumentNullException.ThrowIfNull(summaries);
        if (summaries.Username != Username) throw new System.InvalidOperationException("账号摘要身份不符");
        Summaries = summaries;
    }
}
