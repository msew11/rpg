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

    /// <summary>在请求前建立空缓存，首份和后续响应均在缓存阶段处理。</summary>
    public LoginCache()
    {
        Subscribe<LoginRt>(OnLoggedIn);
    }

    /// <summary>先验证已有摘要，再建立监听，避免构造失败遗留订阅。</summary>
    public LoginCache(LoginRt response)
    {
        OnLoggedIn(response);
        Subscribe<LoginRt>(OnLoggedIn);
    }

    /// <summary>清除账号状态与摘要，供返回登录及重新认证失败时使用。</summary>
    public void Clear()
    {
        IsLoggedIn = false;
        Roles = Array.Empty<RoleSummaryPbData>();
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

    /// <summary>结束登录缓存生命周期时清除账号摘要。</summary>
    protected override void OnDispose() => Clear();
}
