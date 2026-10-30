namespace Rpg.Server.Modules.Login.Utils;

/// <summary>在转交物品请求前校验运行时角色归属。</summary>
internal static class RoleItemCalls
{
    /// <summary>角色身份只来自邮箱运行时元数据。</summary>
    internal static long Owned(LoginContext context)
    { var id = context.Call?.RoleId ?? 0; context.Cache.Summaries.Owned(id); return id; }
}
