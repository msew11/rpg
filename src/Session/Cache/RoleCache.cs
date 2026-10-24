using Rpg.Session.Data;
using System;
using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session.Cache;

/// <summary>通过缓存阶段的响应及推送监听维护当前角色权威快照。</summary>
public sealed class RoleCache : CacheBase
{
    /// <summary>当前选择的角色 ID，未选角或退出角色后为零。</summary>
    public long RoleId { get; private set; }
    /// <summary>由协议字段重新组装的最新数据，不持有 protobuf 对象。</summary>
    private RoleWorldStatePbData? _state;
    /// <summary>是否已经收到成功选角的初始状态。</summary>
    public bool IsInitialized => _state is not null;
    /// <summary>最新权威快照；选角响应前不可读取。</summary>
    public RoleWorldStatePbData State => _state ?? throw new InvalidOperationException("角色缓存尚未初始化");

    /// <summary>登录后建立角色协议监听，选角前不持有角色数据。</summary>
    public RoleCache()
    {
        Subscribe<SelectRoleRt>(OnSelected);
        Subscribe<MoveRt>(OnMoved);
        Subscribe<RoleStateNotice>(OnNotice);
    }

    /// <summary>切换角色时重置全部角色数据，零表示回到未选角状态。</summary>
    internal void Reset(long roleId = 0)
    {
        RoleId = roleId;
        _state = null;
    }

    /// <summary>成功选角后接纳首份快照，并检查响应身份一致。</summary>
    private void OnSelected(SelectRoleRt response)
    {
        if (RoleId == 0 || !response.Success || response.RoleId != RoleId) return;
        if (response.State is null || response.State.RoleId != response.RoleId) throw new InvalidDataException("选角响应角色状态不一致");
        Apply(response.State, true);
    }

    /// <summary>仅使用成功且属于当前角色的移动响应。</summary>
    private void OnMoved(MoveRt response)
    {
        if (RoleId == 0 || !response.Success || response.RoleId != RoleId) return;
        if (response.State is null || response.State.RoleId != response.RoleId || response.ActionId != response.State.AcknowledgedAction)
            throw new InvalidDataException("移动响应角色或确认序号不一致");
        Apply(response.State, false);
    }

    /// <summary>从主动推送接纳当前角色状态。</summary>
    private void OnNotice(RoleStateNotice notice)
    {
        if (RoleId == 0) return;
        if (notice.State is null) throw new InvalidDataException("角色推送缺少状态");
        Apply(notice.State, false);
    }

    /// <summary>按角色及 revision 过滤，只有选角响应可以初始化空缓存。</summary>
    private void Apply(RoleWorldStatePb message, bool initialize)
    {
        if (message.RoleId != RoleId || (_state is null && !initialize) ||
            (_state is not null && message.Revision <= _state.Revision)) return;
        _state = RoleWorldStatePbData.FromPb(message);
    }

    /// <summary>账号会话释放时清除当前角色数据。</summary>
    protected override void OnDispose() => Reset();
}
