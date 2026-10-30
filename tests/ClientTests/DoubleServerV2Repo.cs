using System.Collections.Immutable;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Modules.World.Storage;

namespace ClientTests;

/// <summary>服务 openspec/specs/server-v2-runtime 的纯内存仓库契约，网络框架测试不访问文件。</summary>
internal sealed class DoubleServerV2Repo : IAccountRoleSummaryRepo, IRoleRepo, IWorldRepo
{
    /// <summary>本替身只服务框架，不提供业务取号能力。</summary>
    public ValueTask<long> NextRoleIdAsync() => throw new NotSupportedException();
    /// <summary>返回指定账号的空基线。</summary>
    public ValueTask<ImmutableArray<DataRoleSummary>> LoadAsync(string username) => ValueTask.FromResult(ImmutableArray<DataRoleSummary>.Empty);
    /// <summary>接纳框架测试的摘要写入，不持久化。</summary>
    public ValueTask SaveAsync(string username, ImmutableArray<DataRoleSummary> summaries) => ValueTask.CompletedTask;
    /// <summary>身份测试只需独立的角色基线。</summary>
    public ValueTask<DataRoleSnapshot> LoadAsync(long roleId) => ValueTask.FromResult(new DataRoleSnapshot(new DataRole { RoleId = roleId }, [], []));
    /// <summary>接纳框架测试的三表写入。</summary>
    public ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables) => ValueTask.CompletedTask;
    /// <summary>地图框架测试的空对象基线。</summary>
    public ValueTask<DataWorldSnapshot> LoadAsync(DataWorldLayout layout) => ValueTask.FromResult(new DataWorldSnapshot([]));
    /// <summary>接纳测试地图写入，不执行 IO。</summary>
    public ValueTask SaveAsync(DataWorldLayout layout, DataWorldSnapshot snapshot, ImmutableHashSet<DataWorldChunk> dirtyChunks) => ValueTask.CompletedTask;
}
