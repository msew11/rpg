using Rpg.Net;
using Rpg.Session.Data;
using System;
using System.IO;
using Rpg.Protocol;
using Rpg.Services;

namespace Rpg.Session.Cache;

/// <summary>通过缓存阶段的响应及推送监听维护当前角色权威快照。</summary>
public sealed class RoleCache : CacheBase
{
    /// <summary>当前选择的角色 ID，未选角或退出角色后为零。</summary>
    public long RoleId { get; private set; }
    /// <summary>由协议字段重新组装的最新数据，不持有 protobuf 对象。</summary>
    private RoleWorldStatePbData? _state;
    /// <summary>当前角色的独立背包快照。</summary>
    private BagPbData? _bag;
    /// <summary>选角完成后的只读完整背包。</summary>
    public BagPbData Bag => _bag ?? throw new InvalidOperationException("背包缓存尚未初始化");
    /// <summary>当前角色独立公开的完整装备。</summary>
    public RoleWorldEquipmentPbData Equipment => State.Equipment;
    /// <summary>是否已经收到成功选角的初始状态。</summary>
    public bool IsInitialized => _state is not null;
    /// <summary>最新权威快照；选角响应前不可读取。</summary>
    public RoleWorldStatePbData State => _state ?? throw new InvalidOperationException("角色缓存尚未初始化");

    /// <summary>世界协调器完成整批校验后接纳本人位置，不覆盖私有数据。</summary>
    internal void AcceptPosition(RoleWorldPositionPbData position)
    {
        _state = State with { Position = position.Position, PositionRevision = position.Revision, AcknowledgedAction = position.AcknowledgedAction, Target = position.Target, Speed = position.Speed };
    }

    /// <summary>完整组合已验证后一次替换物品和外观。</summary>
    internal void AcceptItems(BagPbData bag, RoleWorldEquipmentPbData equipment)
    { _bag = bag; _state = State with { Equipment = equipment }; }

    /// <summary>账号会话释放时清除当前角色数据。</summary>
    protected override void OnDispose() => Reset();

    /// <summary>切换角色时清空全部缓存。</summary>
    internal void Reset() { RoleId = 0; _state = null; _bag = null; }

    /// <summary>完整入口准备成功后一次接纳角色和物品。</summary>
    internal void AcceptEntry(RoleWorldStatePbData state, BagPbData bag)
    { RoleId = state.RoleId; _state = state; _bag = bag; }
}
