using System.Threading.Tasks;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role;

namespace Rpg.Server.Modules.Role.Storage;

/// <summary>以角色为边界加载及共同提交三表，不缓存角色状态。</summary>
public interface IRoleStorage
{
    /// <summary>恢复未完成提交后加载全部必需表。</summary>
    ValueTask<DataRoleSnapshot> LoadAsync(long roleId);
    /// <summary>分类加载；仅三表全部不存在返回 null，默认存储保持严格加载。</summary>
    async ValueTask<DataRoleSnapshot?> TryLoadExistingAsync(long roleId) => await LoadAsync(roleId).ConfigureAwait(false);
    /// <summary>共同提交本轮变化表；失败由 Actor 保留内存供重试。</summary>
    ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables);
}
