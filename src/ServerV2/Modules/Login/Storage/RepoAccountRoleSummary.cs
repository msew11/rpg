using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Rpg.ServerV2.Modules.Common.Storage;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login;

namespace Rpg.ServerV2.Modules.Login.Storage;

/// <summary>每账号一份摘要数组的文件仓库，不持有账号业务状态。</summary>
public sealed class RepoAccountRoleSummary : IAccountRoleSummaryRepo
{
    /// <summary>宿主提供的 V2 专用绝对目录。</summary>
    private readonly string _directory;
    /// <summary>绑定专用目录，读取前不创建文件夹。</summary>
    public RepoAccountRoleSummary(string directory) => _directory = Path.GetFullPath(directory);
    /// <summary>只有文件或目录不存在时返回空摘要，其他 IO 错误继续传播。</summary>
    public async ValueTask<DataAccountRoleSummaries> LoadAsync(string username)
    {
        var key = UsernameKey.Normalize(username);
        try
        {
            var roles = await StorageJson.ReadAsync<ImmutableArray<DataRoleSummary>>(Path.Combine(_directory, UsernameKey.SummaryFileName(key))).ConfigureAwait(false);
            return new(key, roles);
        }
        catch (FileNotFoundException) { return new(key, []); }
        catch (DirectoryNotFoundException) { return new(key, []); }
    }
    /// <summary>仅写数组，账号身份固定来自安全文件名。</summary>
    public ValueTask SaveAsync(DataAccountRoleSummaries summaries)
    {
        System.ArgumentNullException.ThrowIfNull(summaries);
        return StorageJson.WriteAsync(Path.Combine(_directory, UsernameKey.SummaryFileName(summaries.Username)), StorageJson.Encode(summaries.Roles));
    }
}
