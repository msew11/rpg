using System.Collections.Immutable;
using System.Linq;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Role.Data;

namespace Rpg.ServerV2.Modules.Role;

/// <summary>角色分片独占的三表状态及上次保存基线，不执行 IO。</summary>
public sealed class RoleContext : ModuleContext
{
    /// <summary>全局角色身份，不以连接 ID 代替。</summary>
    public long RoleId { get; }
    /// <summary>当前角色基础数据。</summary>
    public DataRole Role { get; private set; }
    /// <summary>当前不可变物品表。</summary>
    public ImmutableArray<DataItem> Items { get; private set; }
    /// <summary>当前不可变装备扩展表。</summary>
    public ImmutableArray<DataEquipment> Equipments { get; private set; }
    /// <summary>上次成功保存的完整快照，仅共享不可变数据。</summary>
    private DataRoleSnapshot _saved;
    /// <summary>相对于保存基线发生变化的表。</summary>
    public DataRoleTables DirtyTables { get; private set; }
    /// <summary>由所属 Actor 用完整加载结果建立基线。</summary>
    internal RoleContext(long roleId, DataRoleSnapshot snapshot, ConfigService? config = null) : base(config)
    {
        RoleId = roleId;
        Role = snapshot.Role;
        Items = snapshot.Items;
        Equipments = snapshot.Equipments;
        _saved = snapshot;
    }
    /// <summary>当前三表的不可变传输快照。</summary>
    public DataRoleSnapshot Snapshot() => new(Role, Items, Equipments);
    /// <summary>回合内一次替换三表，按内容跟踪变化，不验证业务字段。</summary>
    public void Commit(DataRoleSnapshot snapshot)
    {
        Role = snapshot.Role;
        Items = snapshot.Items.IsDefault ? [] : snapshot.Items;
        Equipments = snapshot.Equipments.IsDefault ? [] : snapshot.Equipments;
        DirtyTables = (Role == _saved.Role ? DataRoleTables.None : DataRoleTables.Role)
            | (Items.SequenceEqual(_saved.Items) ? DataRoleTables.None : DataRoleTables.Item)
            | (Equipments.SequenceEqual(_saved.Equipments) ? DataRoleTables.None : DataRoleTables.Equipment);
    }
    /// <summary>仅在所属 Actor 确认仓库成功后接受当前基线。</summary>
    internal void AcceptSaved() { _saved = Snapshot(); DirtyTables = DataRoleTables.None; }
}
