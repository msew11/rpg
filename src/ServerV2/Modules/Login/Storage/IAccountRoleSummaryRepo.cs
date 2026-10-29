using System.Collections.Immutable;
using System.Threading.Tasks;
using Rpg.ServerV2.Modules.Login.Data;

namespace Rpg.ServerV2.Modules.Login.Storage;

/// <summary>每账号一份摘要的加载与完整保存契约，不持有业务状态。</summary>
public interface IAccountRoleSummaryRepo
{
    /// <summary>加载指定规范账号的全部角色摘要，不扫描其他账号。</summary>
    ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username);
    /// <summary>以账号为边界保存完整摘要，不拆成按角色命名的摘要文件。</summary>
    ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries);
}
