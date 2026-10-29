namespace Rpg.ServerV2.Modules.Role;

/// <summary>角色分片的身份边界，后续角色状态由本上下文独占。</summary>
public sealed class RoleContext
{
    /// <summary>全局角色身份，不以连接 ID 代替。</summary>
    public long RoleId { get; }
    /// <summary>由所属 Actor 初始化固定身份。</summary>
    internal RoleContext(long roleId) => RoleId = roleId;
}
