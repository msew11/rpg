using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Net.Message;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>维护账号登录状态与角色摘要，跨角色会话保留，返回登录时清空。</summary>
public sealed class LoginCache : CacheBase
{
    /// <summary>是否已收到有效的成功登录响应。</summary>
    public bool IsLoggedIn { get; private set; }
    /// <summary>当前账号可选择的不可变角色摘要列表。</summary>
    public IReadOnlyList<RoleSummaryPbData> Roles { get; private set; } = Array.Empty<RoleSummaryPbData>();

    /// <summary>当前唯一在途 V2 操作期待的回复类型，空值表示可以提交下一次操作。</summary>
    private Type? _v2Reply;
    /// <summary>当前 V2 账号操作是否尚未完成。</summary>
    internal bool V2RequestPending => _v2Reply is not null;

    /// <summary>在请求前建立空缓存，首份和后续响应均在缓存阶段处理。</summary>
    public LoginCache() : this(false) { }

    /// <summary>按当前传输订阅单一版本的账号协议，避免混用旧响应。</summary>
    internal LoginCache(bool useV2)
    {
        if (useV2)
        {
            Subscribe<ProtocolV2.LoginRt>(OnV2LoggedIn);
            Subscribe<ProtocolV2.CreateRoleRt>(reply => OnV2Roles(reply.Roles, typeof(ProtocolV2.CreateRoleRt)));
            Subscribe<ProtocolV2.DeleteRoleRt>(reply => OnV2Roles(reply.Roles, typeof(ProtocolV2.DeleteRoleRt)));
            Subscribe<ProtocolV2.ErrorRt>(_ => CancelV2Request());
            return;
        }
        Subscribe<LoginRt>(OnLoggedIn);
        Subscribe<CreateRoleRt>(OnCreated);
        Subscribe<DeleteRoleRt>(OnDeleted);
    }

    /// <summary>先验证已有摘要，再建立监听，避免构造失败遗留订阅。</summary>
    public LoginCache(LoginRt response)
    {
        OnLoggedIn(response);
        Subscribe<LoginRt>(OnLoggedIn);
        Subscribe<CreateRoleRt>(OnCreated);
        Subscribe<DeleteRoleRt>(OnDeleted);
    }

    /// <summary>清除账号状态与摘要，供返回登录及重新认证失败时使用。</summary>
    public void Clear()
    {
        _v2Reply = null;
        IsLoggedIn = false;
        Roles = Array.Empty<RoleSummaryPbData>();
    }

    /// <summary>先登记期待回复，再发请求；同一客户端不能并发管理账号。</summary>
    internal void BeginV2Request<T>()
    {
        if (V2RequestPending) throw new InvalidOperationException("请等待当前账号操作完成");
        _v2Reply = typeof(T);
    }

    /// <summary>进入准备期间核对当前唯一在途响应。</summary>
    internal bool ExpectsV2<T>() => _v2Reply == typeof(T);
    /// <summary>提交已经准备好的摘要候选，不再执行转换。</summary>
    internal void AcceptEntry(IReadOnlyList<RoleSummaryPbData> roles) => Roles = roles;

    /// <summary>统一失败或传输、缓存错误解除等待，不破坏已接纳的列表。</summary>
    internal void CancelV2Request() => _v2Reply = null;

    /// <summary>首份有效快照完整校验后才建立账号，非法内容不留下部分摘要。</summary>
    private void OnV2LoggedIn(ProtocolV2.LoginRt reply)
    {
        if (_v2Reply != typeof(ProtocolV2.LoginRt)) return;
        Clear();
        ReplaceV2Roles(reply.Roles);
        IsLoggedIn = true;
    }

    /// <summary>只接纳当前在途操作，转换失败仍释放等待并保留原列表。</summary>
    private void OnV2Roles(IEnumerable<ProtocolV2.RoleSummaryPb> roles, Type replyType)
    {
        if (!IsLoggedIn || _v2Reply != replyType) return;
        try { ReplaceV2Roles(roles); }
        finally { CancelV2Request(); }
    }

    /// <summary>复制全部 V2 摘要并拒绝重复编号，最后一次性发布不可变列表。</summary>
    private void ReplaceV2Roles(IEnumerable<ProtocolV2.RoleSummaryPb> messages)
    {
        var roles = messages.Select(RoleSummaryPbData.FromPb).ToArray();
        if (roles.Select(role => role.RoleId).Distinct().Count() != roles.Length)
            throw new System.IO.InvalidDataException("角色列表包含重复 ID");
        Roles = Array.AsReadOnly(roles);
    }

    /// <summary>完整转换成功响应后更新摘要，失败或转换异常不保留旧账号数据。</summary>
    private void OnLoggedIn(LoginRt response)
    {
        Clear();
        if (!response.Success) return;
        var roles = response.Roles.Select(RoleSummaryPbData.FromPb).ToArray();
        Roles = Array.AsReadOnly(roles);
        IsLoggedIn = true;
    }

    /// <summary>创建成功时在缓存阶段替换完整角色列表。</summary>
    private void OnCreated(CreateRoleRt response)
    {
        if (IsLoggedIn && response.Success) ReplaceRoles(response.Roles);
    }

    /// <summary>删除成功时接纳完整列表，包括空列表。</summary>
    private void OnDeleted(DeleteRoleRt response)
    {
        if (IsLoggedIn && response.Success) ReplaceRoles(response.Roles);
    }

    /// <summary>完整转换并拒绝重复 ID，失败不污染旧列表。</summary>
    private void ReplaceRoles(IEnumerable<RoleSummaryPb> messages)
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
