using System.Threading.Tasks;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Role;

namespace Rpg.ServerV2.Modules.Role.Storage;

/// <summary>以角色为边界加载及共同提交三表，不缓存角色状态。</summary>
public interface IRoleRepo
{
    /// <summary>恢复未完成提交后加载全部必需表。</summary>
    ValueTask<DataRoleSnapshot> LoadAsync(long roleId);
    /// <summary>共同提交本轮变化表；失败由 Actor 保留内存供重试。</summary>
    ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables);
}
