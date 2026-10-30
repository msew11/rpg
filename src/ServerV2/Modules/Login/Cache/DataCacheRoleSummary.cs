using System;
using System.Collections.Immutable;
using System.Linq;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Modules.Common.Cache;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Protocol;

namespace Rpg.ServerV2.Modules.Login.Cache;

/// <summary>账号全部摘要及墓碑的独占工作区，内聚角色列表管理和保存比较。</summary>
public sealed class DataCacheRoleSummary : DataCacheArray<DataRoleSummary>
{
    /// <summary>以完整加载结果建立基线，摘要装备列表按内容比较。</summary>
    internal DataCacheRoleSummary(ImmutableArray<DataRoleSummary> value) : base(value, SummaryComparer.Instance) { }
    /// <summary>提供按编号排序的未删除摘要，不返回可变集合。</summary>
    public ImmutableArray<DataRoleSummary> Visible() => Value.Where(role => !role.IsDeleted).OrderBy(role => role.RoleId).ToImmutableArray();
    /// <summary>可信进入前查找账号未删除摘要，非法编号与不存在分别报告。</summary>
    public DataRoleSummary Owned(long roleId)
    {
        if (roleId <= 0) throw new ServerRequestException(ErrorCodePb.InvalidRequest, "角色编号必须为正整数");
        return Value.FirstOrDefault(role => role.RoleId == roleId && !role.IsDeleted)
            ?? throw new ServerRequestException(ErrorCodePb.RoleNotFound, "角色不存在或已删除");
    }
    /// <summary>相关业务成功时从实际公开状态同步展示字段，不依赖离开世界。</summary>
    internal void Synchronize(RoleWorldStatePb state)
    {
        var owned = Owned(state.RoleId);
        var updated = owned with { Name = state.Profile.Name, Level = state.Profile.Level,
            Gender = (RoleGender)state.Profile.Gender, HeadId = state.Profile.HeadId,
            EquippedItems = state.Equipment.EquippedItems.ToImmutableArray() };
        Commit(Value.Replace(owned, updated));
    }
    /// <summary>取号前校验创建意图；不修改摘要，返回规范角色名。</summary>
    public string ValidateCreation(string requestedName, RoleGender gender, int headId)
    {
        var name = requestedName.Trim();
        if (name.Length is < 1 or > 16 || name.Any(char.IsControl))
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "角色名字需为 1 至 16 个字符，且不能包含控制字符");
        if (gender is not (RoleGender.Male or RoleGender.Female) || headId is < 1 or > 74)
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "性别或头型无效");
        if (Value.Any(role => !role.IsDeleted && string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ServerRequestException(ErrorCodePb.RoleNameConflict, "角色名字已存在");
        return name;
    }
    /// <summary>使用基础设施已分配的编号一次提交摘要，不在缓存内生成编号。</summary>
    public void Create(long roleId, string requestedName, RoleGender gender, int headId)
    {
        var name = ValidateCreation(requestedName, gender, headId);
        if (roleId <= 0 || Value.Any(role => role.RoleId == roleId))
            throw new InvalidOperationException("角色编号尚未分配或已被使用");
        var summary = new DataRoleSummary(roleId, name, 1,
            gender, headId, []);
        Commit(Value.Add(summary));
    }
    /// <summary>仅将属于本账号的可见摘要标记为墓碑，不触碰角色三表。</summary>
    public void Delete(long roleId)
    {
        if (roleId <= 0)
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "角色编号必须为正整数");
        var index = -1;
        for (var i = 0; i < Value.Length; i++)
            if (Value[i] is { IsDeleted: false } role && role.RoleId == roleId) { index = i; break; }
        if (index < 0) throw new ServerRequestException(ErrorCodePb.RoleNotFound, "角色不存在或已删除");
        Commit(Value.SetItem(index, Value[index] with { IsDeleted = true }));
    }
    /// <summary>摘要中的嵌套不可变数组也按内容比较，避免重新构造导致误写。</summary>
    private sealed class SummaryComparer : System.Collections.Generic.IEqualityComparer<DataRoleSummary>
    {
        /// <summary>无状态比较器。</summary>
        internal static readonly SummaryComparer Instance = new();
        /// <summary>比较展示字段与装备配置编号顺序。</summary>
        public bool Equals(DataRoleSummary? left, DataRoleSummary? right) => ReferenceEquals(left, right) ||
            left is not null && right is not null && left.RoleId == right.RoleId && left.Name == right.Name &&
            left.Level == right.Level && left.Gender == right.Gender && left.HeadId == right.HeadId && left.IsDeleted == right.IsDeleted &&
            (left.EquippedItems.IsDefault ? [] : left.EquippedItems).SequenceEqual(right.EquippedItems.IsDefault ? [] : right.EquippedItems);
        /// <summary>身份哈希与内容相等契约兼容；当前仅用于顺序比较。</summary>
        public int GetHashCode(DataRoleSummary value) => value.RoleId.GetHashCode();
    }
}
