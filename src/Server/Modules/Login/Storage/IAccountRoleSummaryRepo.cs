using System.Collections.Immutable;
using System.Threading.Tasks;
using Rpg.Server.Modules.Login.Data;

namespace Rpg.Server.Modules.Login.Storage;

/// <summary>账号摘要加载、保存及全服角色编号预留契约，不持有账号业务状态。</summary>
public interface IAccountRoleSummaryRepo
{
    /// <summary>从共享持久化序列取号，不以账号划分区间或依赖摘要保存。</summary>
    ValueTask<long> NextRoleIdAsync();
    /// <summary>加载指定规范账号的全部角色摘要，不扫描其他账号。</summary>
    ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username);
    /// <summary>以账号为边界保存完整摘要，不拆成按角色命名的摘要文件。</summary>
    ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries);
}
