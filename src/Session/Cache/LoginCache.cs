using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Protocol;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>维护账号登录状态与角色摘要，跨角色会话保留，返回登录时清空。</summary>
public sealed class LoginCache : CacheBase
{
    /// <summary>是否已收到有效的成功登录响应。</summary>
    public bool IsLoggedIn { get; private set; }
    /// <summary>当前账号可选择的不可变角色摘要列表。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles { get; private set; } = Array.Empty<RoleSummaryPbData>();

    /// <summary>当前唯一在途 操作期待的回复类型，空值表示可以提交下一次操作。</summary>
    private Type? _reply;
    /// <summary>当前 账号操作是否尚未完成。</summary>
    internal bool RequestPending => _reply is not null;

    /// <summary>订阅正式账号协议并维护摘要及请求状态。</summary>
    public LoginCache()
    {
        Subscribe<global::Rpg.Protocol.SessionKickedNotice>(_ => Clear());
        Subscribe<global::Rpg.Protocol.LoginRt>(OnLoggedIn);
        Subscribe<global::Rpg.Protocol.CreateRoleRt>(reply => OnRoles(reply.Roles, typeof(global::Rpg.Protocol.CreateRoleRt)));
        Subscribe<global::Rpg.Protocol.DeleteRoleRt>(reply => OnRoles(reply.Roles, typeof(global::Rpg.Protocol.DeleteRoleRt)));
        Subscribe<global::Rpg.Protocol.ErrorRt>(_ => CancelRequest());
    }

    /// <summary>清除账号状态与摘要，供返回登录及重新认证失败时使用。</summary>
    public void Clear()
    {
        _reply = null;
        IsLoggedIn = false;
        Roles = Array.Empty<RoleSummaryPbData>();
    }

    /// <summary>先登记期待回复，再发请求；同一客户端不能并发管理账号。</summary>
    internal void BeginRequest<T>()
    {
        if (RequestPending) throw new InvalidOperationException("请等待当前账号操作完成");
        _reply = typeof(T);
    }

    /// <summary>进入准备期间核对当前唯一在途响应。</summary>
    internal bool Expects<T>() => _reply == typeof(T);
    /// <summary>提交已经准备好的摘要候选，不再执行转换。</summary>
    internal void AcceptEntry(IReadOnlyList<RoleSummaryPbData> roles) => Roles = roles;

    /// <summary>统一失败或传输、缓存错误解除等待，不破坏已接纳的列表。</summary>
    internal void CancelRequest() => _reply = null;

    /// <summary>首份有效快照完整校验后才建立账号，非法内容不留下部分摘要。</summary>
    private void OnLoggedIn(global::Rpg.Protocol.LoginRt reply)
    {
        if (_reply != typeof(global::Rpg.Protocol.LoginRt)) return;
        Clear();
        ReplaceRoles(reply.Roles);
        IsLoggedIn = true;
    }

    /// <summary>只接纳当前在途操作，转换失败仍释放等待并保留原列表。</summary>
    private void OnRoles(IEnumerable<global::Rpg.Protocol.RoleSummaryPb> roles, Type replyType)
    {
        if (!IsLoggedIn || _reply != replyType) return;
        try { ReplaceRoles(roles); }
        finally { CancelRequest(); }
    }

    /// <summary>复制全部 摘要并拒绝重复编号，最后一次性发布不可变列表。</summary>
    private void ReplaceRoles(IEnumerable<global::Rpg.Protocol.RoleSummaryPb> messages)
    {
        var roles = messages.Select(RoleSummaryPbData.FromPb).ToArray();
        if (roles.Select(role => role.RoleId).Distinct().Count() != roles.Length)
            throw new System.IO.InvalidDataException("角色列表包含重复 ID");
        Roles = Array.AsReadOnly(roles);
    }

    /// <summary>离开角色时从已校验缓存刷新列表展示，避免预览保留旧装备。</summary>
    internal void RefreshRole(RoleWorldStatePbData state)
    {
        var previous = Roles.FirstOrDefault(role => role.RoleId == state.RoleId);
        if (previous is null || previous.Name == state.Name && previous.Level == state.Level &&
            previous.Gender == state.Gender && previous.HeadId == state.HeadId &&
            previous.EquippedItems.SequenceEqual(state.Equipment.EquippedItems)) return;
        var summary = new RoleSummaryPbData(state.RoleId, state.Name, state.Level, state.Gender, state.HeadId)
        { EquippedItems = Array.AsReadOnly(state.Equipment.EquippedItems.ToArray()) };
        Roles = Array.AsReadOnly(Roles.Select(role => role.RoleId == state.RoleId ? summary : role).ToArray());
    }

    /// <summary>结束登录缓存生命周期时清除账号摘要。</summary>
    protected override void OnDispose() => Clear();
}
