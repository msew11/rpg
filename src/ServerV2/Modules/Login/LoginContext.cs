using System.Linq;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Login.Data;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>单个账号 Actor 的独占内存上下文，不包含连接认证状态。</summary>
public sealed class LoginContext : ModuleContext
{
    /// <summary>当前分片的规范账号身份。</summary>
    public string Username { get; }
    /// <summary>本账号持有的不可变摘要快照。</summary>
    public DataAccountRoleSummaries Summaries { get; private set; }
    /// <summary>上次成功保存的不可变摘要基线。</summary>
    private DataAccountRoleSummaries _saved;
    /// <summary>当前摘要与保存基线是否不同。</summary>
    public bool SummariesDirty { get; private set; }
    /// <summary>用所属 Actor 完整加载的摘要建立基线，不执行 IO。</summary>
    internal LoginContext(string username, DataAccountRoleSummaries? summaries = null, ConfigService? config = null) : base(config)
    { Username = username; Summaries = _saved = summaries ?? new(username, []); }
    /// <summary>仅在所属回合接纳新快照，以本 Context 的固定账号键持有数据。</summary>
    public void ReplaceSummaries(DataAccountRoleSummaries summaries)
    {
        System.ArgumentNullException.ThrowIfNull(summaries);
        Summaries = new(Username, summaries.Roles);
        SummariesDirty = !Summaries.Roles.SequenceEqual(_saved.Roles, SummaryComparer.Instance);
    }
    /// <summary>仓库成功后更新基线，失败时不得调用。</summary>
    internal void AcceptSaved() { _saved = Summaries; SummariesDirty = false; }
    /// <summary>摘要中的嵌套不可变数组也按内容比较，避免重新构造导致误写。</summary>
    private sealed class SummaryComparer : System.Collections.Generic.IEqualityComparer<DataRoleSummary>
    {
        /// <summary>无状态比较器。</summary>
        internal static readonly SummaryComparer Instance = new();
        /// <summary>比较展示字段与装备配置编号顺序。</summary>
        public bool Equals(DataRoleSummary? left, DataRoleSummary? right) => ReferenceEquals(left, right) ||
            left is not null && right is not null && left.RoleId == right.RoleId && left.Name == right.Name &&
            left.Level == right.Level && left.Gender == right.Gender && left.HeadId == right.HeadId &&
            (left.EquippedItems.IsDefault ? [] : left.EquippedItems).SequenceEqual(right.EquippedItems.IsDefault ? [] : right.EquippedItems);
        /// <summary>身份哈希与内容相等契约兼容；当前仅用于顺序比较。</summary>
        public int GetHashCode(DataRoleSummary value) => value.RoleId.GetHashCode();
    }
}
