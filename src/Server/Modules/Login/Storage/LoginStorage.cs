using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Rpg.Server.Modules.Common.Storage;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login;

namespace Rpg.Server.Modules.Login.Storage;

/// <summary>每账号一份摘要数组的文件存储，统一提供与账号无关的持久化取号入口。</summary>
public sealed class LoginStorage : ILoginStorage
{
    /// <summary>宿主提供的 专用绝对目录。</summary>
    private readonly string _directory;
    /// <summary>所有账号共享的持久化编号生成器。</summary>
    private readonly RoleIdGenerator _roleIds;
    /// <summary>绑定专用目录，读取前不创建文件夹。</summary>
    public LoginStorage(string directory)
    { _directory = Path.GetFullPath(directory); _roleIds = new(_directory); }
    /// <summary>先持久化预留编号段，再交付一个不重复的角色编号。</summary>
    public ValueTask<long> NextRoleIdAsync() => _roleIds.NextAsync();
    /// <summary>只有文件或目录不存在时返回空摘要，其他 IO 错误继续传播。</summary>
    public async ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username)
    {
        var fileName = UsernameKey.SummaryFileName(username);
        try
        {
            return await StorageJson.ReadAsync<ImmutableArray<DataRoleSummary>>(Path.Combine(_directory, fileName)).ConfigureAwait(false);
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }
    /// <summary>仅写数组，账号身份固定来自安全文件名。</summary>
    public ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries)
    {
        var fileName = UsernameKey.SummaryFileName(username);
        return StorageJson.WriteAsync(Path.Combine(_directory, fileName), StorageJson.Encode(summaries.IsDefault ? [] : summaries));
    }
}
