using System;
using Rpg.ServerV2.Modules.Role.Data;

namespace Rpg.ServerV2.Modules.Role.Cache;

/// <summary>角色邮箱独占的三表工作区，不执行 IO。</summary>
public sealed class RoleCache
{
    /// <summary>临时移动领域，不参与三表落盘。</summary>
    public DataCacheMovement Movement { get; } = new();
    /// <summary>固定角色身份，不使用连接编号。</summary>
    public long RoleId { get; }
    /// <summary>角色基础表。</summary>
    public DataCacheRole Role => _role ?? throw new InvalidOperationException("角色尚未建档");
    /// <summary>首次可信准备前不持有虚构角色数据。</summary>
    private DataCacheRole? _role;
    /// <summary>物品实例表。</summary>
    public DataCacheItem Items => _items ?? throw new InvalidOperationException("角色尚未建档");
    /// <summary>尚未建档时不构造物品表。</summary>
    private DataCacheItem? _items;
    /// <summary>装备扩展表。</summary>
    public DataCacheEquipment Equipments => _equipments ?? throw new InvalidOperationException("角色尚未建档");
    /// <summary>尚未建档时不构造装备表。</summary>
    private DataCacheEquipment? _equipments;
    /// <summary>是否已有完整加载或可信建立的三表候选。</summary>
    public bool IsInitialized => _role is not null;
    /// <summary>直接汇总各表缓存的待写状态。</summary>
    public DataRoleTables DirtyTables => !IsInitialized ? DataRoleTables.None : (Role.IsDirty ? DataRoleTables.Role : DataRoleTables.None)
        | (Items.IsDirty ? DataRoleTables.Item : DataRoleTables.None)
        | (Equipments.IsDirty ? DataRoleTables.Equipment : DataRoleTables.None);
    /// <summary>用完整加载的三表建立工作区和保存基线。</summary>
    internal RoleCache(long roleId, DataRoleSnapshot? snapshot)
    { RoleId = roleId; if (snapshot is not null) SetTables(snapshot); }
    /// <summary>一次建立三表引用，避免发布部分候选。</summary>
    private void SetTables(DataRoleSnapshot snapshot)
    { _role = new(snapshot.Role); _items = new(snapshot.Items); _equipments = new(snapshot.Equipments); }
    /// <summary>只允许可信进入流程建立首次候选，初始空数组同样待写。</summary>
    internal void Initialize(DataRoleSnapshot snapshot)
    {
        if (IsInitialized) return;
        SetTables(snapshot);
        Role.MarkUnsaved(); Items.MarkUnsaved(); Equipments.MarkUnsaved();
    }
    /// <summary>可信摘要才可建立首次三表，已有候选保持原值供保存重试。</summary>
    internal void Prepare(Rpg.ProtocolV2.RoleSummaryPb summary, Rpg.Config.ConfigService config)
    {
        if (summary is null || summary.RoleId != RoleId || summary.RoleId <= 0)
            throw new System.IO.InvalidDataException("可信摘要身份不一致");
        if (!IsInitialized)
        {
            if (!config.Map.Items.TryGetValue(config.Basic.Value.InitialMapId, out var map))
                throw new System.IO.InvalidDataException("出生地图配置不存在");
            Initialize(new(DataCacheRole.CreateInitial(summary, map), [], []));
        }
    }
    /// <summary>导出不可变三表，后续提交不修改导出值。</summary>
    public DataRoleSnapshot Snapshot() => new(Role.Value, Items.Value, Equipments.Value);
    /// <summary>回合内一次接纳三表，先检查结构再修改缓存。</summary>
    public void Commit(DataRoleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Role);
        Role.Commit(snapshot.Role); Items.Commit(snapshot.Items); Equipments.Commit(snapshot.Equipments);
    }
    /// <summary>完整保存成功后共同更新三表基线。</summary>
    internal void AcceptSaved() { Role.AcceptSaved(); Items.AcceptSaved(); Equipments.AcceptSaved(); }
}
