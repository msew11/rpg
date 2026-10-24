using Rpg.Const;

namespace Rpg.World.Components;

/// <summary>角色实体持有的身份组件。</summary>
internal sealed class DataRoleComponent
{
    /// <summary>实体对应的会话角色 ID。</summary>
    public long RoleId { get; }
    /// <summary>当前权威角色数据声明的性别。</summary>
    public RoleGender Gender { get; set; }

    /// <summary>当前权威头型编号。</summary>
    public int HeadId { get; set; }

    /// <summary>按会话角色身份和权威性别构造组件。</summary>
    public DataRoleComponent(long roleId, RoleGender gender, int headId)
    {
        RoleId = roleId;
        Gender = gender;
        HeadId = headId;
    }
}
