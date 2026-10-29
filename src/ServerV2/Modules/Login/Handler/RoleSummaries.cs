using System.Collections.Generic;
using System.Linq;
using Rpg.ProtocolV2;

namespace Rpg.ServerV2.Modules.Login.Handler;

/// <summary>将账号拥有的不可变摘要投影为独占的可见协议列表。</summary>
internal static class RoleSummaries
{
    /// <summary>排除墓碑并按编号排序，不暴露持久化集合或修改上下文。</summary>
    internal static IEnumerable<RoleSummaryPb> Visible(LoginContext context) => context.Cache.Summaries.Visible().Select(role =>
        {
            var message = new RoleSummaryPb { RoleId = role.RoleId, Name = role.Name, Level = role.Level,
                Gender = (RoleGenderPb)role.Gender, HeadId = role.HeadId };
            if (!role.EquippedItems.IsDefault) message.EquippedItems.AddRange(role.EquippedItems);
            return message;
        });
}
