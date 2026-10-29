using System.Threading.Tasks;

namespace Rpg.ServerV2.Storage;

/// <summary>每账号一份摘要的 IO 契约；真实文件实现留给后续业务 change。</summary>
public interface IAccountRoleSummaryRepo
{
    /// <summary>加载指定规范账号的全部角色摘要，不扫描其他账号。</summary>
    ValueTask<DataAccountRoleSummaries> LoadAsync(string username);
    /// <summary>以账号为边界保存完整摘要，不拆成按角色命名的摘要文件。</summary>
    ValueTask SaveAsync(DataAccountRoleSummaries summaries);
}
