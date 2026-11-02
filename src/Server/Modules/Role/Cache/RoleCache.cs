using System;
using Rpg.Server.Modules.Role.Data;

namespace Rpg.Server.Modules.Role.Cache;

/// <summary>角色邮箱独占的三表工作区，不执行 IO。</summary>
public sealed class RoleCache
{
    /// <summary>当前待完成的拾取，新的移动或控制屏障静默清除。</summary>
    internal DataRolePickup? Pickup { get; set; }
    /// <summary>当前攻击快照，不保存到角色三表。</summary>
    internal DataRoleAttack? Attack { get; set; }
    /// <summary>取消动作也保留的下次可攻击时刻。</summary>
    internal double NextAttackMs { get; set; }
    /// <summary>最近接纳的战斗输入编号，与移动比较防止乱序。</summary>
    internal ulong CombatInput { get; set; }
    /// <summary>临时移动领域，不参与三表落盘。</summary>
    public DataCacheMovement Movement { get; }
    /// <summary>位置与装备共同维护的公开投影状态。</summary>
    public DataCacheProjection Projection { get; } = new();
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
    /// <summary>已计算的等级，零表示尚未加载正常属性。</summary>
    internal int AttributeLevel { get; set; }
    /// <summary>参与上次属性计算的主手道具编号。</summary>
    internal int AttributeWeapon { get; set; } = -1;
    /// <summary>只读正常属性，不包含 GM 执行覆盖。</summary>
    internal System.Collections.Generic.IReadOnlyDictionary<int, double> Attributes { get; set; } = new System.Collections.Generic.Dictionary<int, double>();
    /// <summary>正常属性发生变化后需要独立通知。</summary>
    internal bool AttributesPending { get; set; }
    /// <summary>是否已有完整加载或可信建立的三表候选。</summary>
    public bool IsInitialized => _role is not null;
    /// <summary>直接汇总各表缓存的待写状态。</summary>
    public DataRoleTables DirtyTables => !IsInitialized ? DataRoleTables.None : (Role.IsDirty ? DataRoleTables.Role : DataRoleTables.None)
        | (Items.IsDirty ? DataRoleTables.Item : DataRoleTables.None)
        | (Equipments.IsDirty ? DataRoleTables.Equipment : DataRoleTables.None);
    /// <summary>用完整加载的三表建立工作区和保存基线。</summary>
    internal RoleCache(long roleId, DataRoleSnapshot? snapshot)
    { RoleId = roleId; Movement = new(Projection); if (snapshot is not null) SetTables(snapshot); }
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
    internal void Prepare(Rpg.Protocol.RoleSummaryPb summary, Rpg.Config.ConfigService config)
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
