using System;
using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session;

/// <summary>通过缓存阶段的响应及推送监听维护当前角色权威快照。</summary>
public sealed class RoleCache : IDisposable
{
    /// <summary>该缓存限定的角色 ID。</summary>
    private readonly long _roleId;
    /// <summary>该功能模块拥有的所有协议订阅。</summary>
    private readonly IDisposable[] _subscriptions;
    /// <summary>选角成功后建立的最新快照。</summary>
    private DataRoleState? _state;
    /// <summary>是否已经收到成功选角的初始状态。</summary>
    public bool IsInitialized => _state is not null;
    /// <summary>最新权威快照；选角响应前不可读取。</summary>
    public DataRoleState State => _state ?? throw new InvalidOperationException("角色缓存尚未初始化");

    /// <summary>在发送选角之前绑定当前角色响应与主动推送。</summary>
    public RoleCache(long roleId, EventSystem events)
    {
        _roleId = roleId;
        _subscriptions = [events.Subscribe<SelectRoleRt>(OnSelected, EventPhase.Cache),
            events.Subscribe<MoveRt>(OnMoved, EventPhase.Cache),
            events.Subscribe<RoleStateNotice>(OnNotice, EventPhase.Cache)];
    }

    /// <summary>释放该模块的全部协议监听。</summary>
    public void Dispose()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
    }

    /// <summary>成功选角后接纳首份快照，并检查响应身份一致。</summary>
    private void OnSelected(SelectRoleRt response)
    {
        if (!response.Success || response.RoleId != _roleId) return;
        if (response.State is null || response.State.RoleId != response.RoleId) throw new InvalidDataException("选角响应角色状态不一致");
        Apply(response.State, true);
    }

    /// <summary>仅使用成功且属于当前角色的移动响应。</summary>
    private void OnMoved(MoveRt response)
    {
        if (!response.Success || response.RoleId != _roleId) return;
        if (response.State is null || response.State.RoleId != response.RoleId || response.ActionId != response.State.AcknowledgedAction)
            throw new InvalidDataException("移动响应角色或确认序号不一致");
        Apply(response.State, false);
    }

    /// <summary>从主动推送接纳当前角色状态。</summary>
    private void OnNotice(RoleStateNotice notice)
    {
        if (notice.State is null) throw new InvalidDataException("角色推送缺少状态");
        Apply(notice.State, false);
    }

    /// <summary>按角色及 revision 过滤，只有选角响应可以初始化空缓存。</summary>
    private void Apply(DataRoleMessage message, bool initialize)
    {
        if (message.RoleId != _roleId || (_state is null && !initialize) ||
            (_state is not null && message.Revision <= _state.Revision)) return;
        _state = RoleMessageMapper.ToState(message);
    }
}
