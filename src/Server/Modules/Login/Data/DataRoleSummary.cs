using System.Collections.Immutable;
using Rpg.Server.Modules.Common.Data;

namespace Rpg.Server.Modules.Login.Data;

/// <summary>账号下角色的不可变摘要，仅承载既有展示字段。</summary>
/// <param name="RoleId">全局角色身份。</param>
/// <param name="Name">角色显示名。</param>
/// <param name="Level">当前等级。</param>
/// <param name="Gender">展示性别。</param>
/// <param name="HeadId">性别内头型编号。</param>
/// <param name="EquippedItems">已穿戴物品的配置编号。</param>
/// <param name="IsDeleted">摘要删除墓碑，保留编号；缺失时为未删除。</param>
public sealed record DataRoleSummary(long RoleId, string Name, int Level, RoleGender Gender, int HeadId,
    ImmutableArray<int> EquippedItems, bool IsDeleted = false);
