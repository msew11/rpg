namespace Rpg.Server.Modules.Role.Utils;

/// <summary>从角色所属配置建立物品规则。</summary>
internal static class RoleItemRules
{
    /// <summary>每回合从所属配置建立独立规则，不持有服务依赖。</summary>
    internal static ItemRules Create(RoleContext context) => new(context.Config.Basic.Value.BagColumns,
        context.Config.Basic.Value.BagRows, context.Config.Item.Items.Values);
}
