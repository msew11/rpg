using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules.Role.Storage;

namespace ClientTests;

/// <summary>服务 openspec/specs/server-v2-runtime 的纯内存仓库契约，网络框架测试不访问文件。</summary>
internal sealed class DoubleServerV2Repo : IAccountRoleSummaryRepo, IRoleRepo
{
    /// <summary>返回指定账号的空基线。</summary>
    public ValueTask<DataAccountRoleSummaries> LoadAsync(string username) => ValueTask.FromResult(new DataAccountRoleSummaries(username, []));
    /// <summary>接纳框架测试的摘要写入，不持久化。</summary>
    public ValueTask SaveAsync(DataAccountRoleSummaries summaries) => ValueTask.CompletedTask;
    /// <summary>身份测试只需独立的角色基线。</summary>
    public ValueTask<DataRoleSnapshot> LoadAsync(long roleId) => ValueTask.FromResult(new DataRoleSnapshot(new DataRole { RoleId = roleId }, [], []));
    /// <summary>接纳框架测试的三表写入。</summary>
    public ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables) => ValueTask.CompletedTask;
}
