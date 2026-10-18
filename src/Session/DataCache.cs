using System;

namespace Rpg.Session;

/// <summary>保存当前会话从状态源获得的最新权威数据。</summary>
public sealed class DataCache
{
    /// <summary>状态变化后通知世界协调器与界面。</summary>
    public event Action<DataRoleState>? RoleChanged;

    /// <summary>当前角色的最新权威状态。</summary>
    public DataRoleState Role { get; private set; }

    /// <summary>以选中角色的初始状态建立缓存。</summary>
    public DataCache(DataRole role) => Role = new DataRoleState(role, role.Position, 0, 0);

    /// <summary>只接纳版本更新的状态并发出通知。</summary>
    public bool Apply(DataRoleState state)
    {
        if (state.Revision <= Role.Revision) return false;
        Role = state;
        RoleChanged?.Invoke(state);
        return true;
    }
}
