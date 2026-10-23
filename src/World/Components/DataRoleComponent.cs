using Rpg.Const;

namespace Rpg.World.Components;

/// <summary>角色实体持有的身份组件。</summary>
internal sealed class DataRoleComponent(long roleId, RoleGender gender)
{
    /// <summary>实体对应的会话角色 ID。</summary>
    public long RoleId { get; } = roleId;
    /// <summary>当前权威角色数据声明的性别。</summary>
    public RoleGender Gender { get; set; } = gender;
}
