using Rpg.Net;
using Rpg.Session.Data;
using System;
using System.IO;
using Rpg.Net.Message;
using Rpg.Services;

namespace Rpg.Session.Cache;

/// <summary>通过缓存阶段的响应及推送监听维护当前角色权威快照。</summary>
public sealed class RoleCache : CacheBase
{
    /// <summary>当前选择的角色 ID，未选角或退出角色后为零。</summary>
    public long RoleId { get; private set; }
    /// <summary>由协议字段重新组装的最新数据，不持有 protobuf 对象。</summary>
    private RoleWorldStatePbData? _state;
    /// <summary>独立版本过滤的角色资料。</summary>
    private RoleWorldProfilePbData? _profile;
    /// <summary>独立版本过滤的生命和魔力状态。</summary>
    private RoleWorldStatusPbData? _status;
    /// <summary>独立版本过滤的角色位置。</summary>
    private RoleWorldPositionPbData? _position;
    /// <summary>当前玩家私有角色领域。</summary>
    private RolePbData? _role;
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

    /// <summary>登录后建立角色协议监听，选角前不持有角色数据。</summary>
    public RoleCache()
    {
        Subscribe<SelectRoleRt>(OnSelected);
        Subscribe<MoveRt>(OnMoved);
        Subscribe<RoleStateNotice>(OnNotice);
        Subscribe<RoleProfileNotice>(OnProfileNotice);
        Subscribe<RolePositionNotice>(OnPositionNotice);
        Subscribe<RoleStatusNotice>(OnStatusNotice);
        Subscribe<RoleChangedNotice>(OnRoleChanged);
        Subscribe<RoleItemsChangedNotice>(OnBagChanged);
    }

    /// <summary>切换角色时重置全部角色数据，零表示回到未选角状态。</summary>
    internal void Reset(long roleId = 0)
    {
        RoleId = roleId;
        _state = null;
        _profile = null;
        _status = null;
        _position = null;
        _role = null;
        _bag = null;
    }

    /// <summary>完整 V2 入口准备成功后一次接纳，后续赋值不再执行可能失败的转换。</summary>
    internal void AcceptEntry(RoleWorldStatePbData state, BagPbData bag)
    {
        RoleId = state.RoleId; _state = state; _bag = bag;
        _profile = new(state.RoleId, state.Name, state.Level, state.Gender, state.HeadId, 0);
        _status = new(state.RoleId, state.Health, state.MaxHealth, state.Mana, state.MaxMana, 0);
        _position = new(state.RoleId, state.MapId, state.Position, 0, 0);
        _role = new(state.RoleId, state.Experience, state.NextExperience, state.BagExtraRows, 0);
    }

    /// <summary>世界协调器完成整批校验后接纳本人位置，不覆盖私有数据。</summary>
    internal void AcceptPosition(RoleWorldPositionPbData position)
    {
        _position = position;
        _state = State with { Position = position.Position, PositionRevision = position.Revision, AcknowledgedAction = position.AcknowledgedAction, Target = position.Target, Speed = position.Speed };
    }

    /// <summary>V2 完整组合已验证后一次替换物品和外观。</summary>
    internal void AcceptItems(BagPbData bag, RoleWorldEquipmentPbData equipment)
    { _bag = bag; _state = State with { Equipment = equipment }; }

    /// <summary>成功选角后接纳首份快照，并检查响应身份一致。</summary>
    private void OnSelected(SelectRoleRt response)
    {
        if (RoleId == 0 || !response.Success || response.RoleId != RoleId) return;
        if (response.Role is null) throw new InvalidDataException("选角响应缺少私有角色数据");
        var state = RoleWorldStatePbData.FromPb(response.State, response.Role, GameServices.Config.Item.Items);
        if (state.RoleId != response.RoleId) throw new InvalidDataException("选角响应角色状态不一致");
        if (_state is not null && state.Revision <= _state.Revision) return;
        var bag = ConvertBag(response.Bag, state.BagExtraRows);
        state.Equipment.ValidateBag(bag);
        _profile = RoleWorldProfilePbData.FromPb(response.State.Profile);
        _status = RoleWorldStatusPbData.FromPb(response.State.Status);
        _position = RoleWorldPositionPbData.FromPb(response.State.Position);
        _role = RolePbData.FromPb(response.Role);
        _state = state;
        _bag = bag;
    }

    /// <summary>仅使用成功且属于当前角色的移动响应。</summary>
    private void OnMoved(MoveRt response)
    {
        if (RoleId == 0 || !response.Success || response.RoleId != RoleId) return;
        if (response.Position is null || response.Position.RoleId != response.RoleId || response.ActionId != response.Position.AcknowledgedAction)
            throw new InvalidDataException("移动响应角色或确认序号不一致");
        ApplyPosition(response.Position);
    }

    /// <summary>从主动推送接纳当前角色状态。</summary>
    private void OnNotice(RoleStateNotice notice)
    {
        if (RoleId == 0) return;
        if (notice.State is null) throw new InvalidDataException("角色推送缺少状态");
        Apply(notice.State);
    }

    /// <summary>接纳当前角色资料的独立推送。</summary>
    private void OnProfileNotice(RoleProfileNotice notice)
    {
        if (RoleId != 0 && notice.Profile is null) throw new InvalidDataException("角色资料推送缺少状态");
        if (notice.Profile is not null) ApplyProfile(notice.Profile);
    }

    /// <summary>接纳当前角色位置的独立推送。</summary>
    private void OnPositionNotice(RolePositionNotice notice)
    {
        if (RoleId != 0 && notice.Position is null) throw new InvalidDataException("角色位置推送缺少状态");
        if (notice.Position is not null) ApplyPosition(notice.Position);
    }

    /// <summary>接纳当前角色可见生命和魔力状态。</summary>
    private void OnStatusNotice(RoleStatusNotice notice)
    {
        if (RoleId != 0 && notice.Status is null) throw new InvalidDataException("角色状态推送缺少状态");
        if (notice.Status is not null) ApplyStatus(notice.Status);
    }

    /// <summary>接纳当前玩家私有角色领域。</summary>
    private void OnRoleChanged(RoleChangedNotice notice)
    {
        if (RoleId != 0 && notice.Role is null) throw new InvalidDataException("私有角色推送缺少数据");
        if (notice.Role is not null) ApplyRole(notice.Role);
    }

    /// <summary>完整推送校验全部公开领域，本人装备保留原子物品渠道的状态。</summary>
    private void Apply(RoleWorldStatePb message)
    {
        if (_state is null) return;
        if (message.Profile is null || message.Status is null || message.Position is null || message.Equipment is null)
            throw new InvalidDataException("完整角色推送缺少资料、状态、位置或装备");
        if (message.Profile.RoleId != RoleId && message.Status.RoleId != RoleId && message.Position.RoleId != RoleId) return;
        if (message.Profile.RoleId != RoleId || message.Status.RoleId != RoleId || message.Position.RoleId != RoleId || message.Equipment.RoleId != RoleId)
            throw new InvalidDataException("完整角色推送身份不一致");
        _ = RoleWorldEquipmentPbData.FromPb(message.Equipment, GameServices.Config.Item.Items);
        var profile = RoleWorldProfilePbData.FromPb(message.Profile);
        var status = RoleWorldStatusPbData.FromPb(message.Status);
        var position = RoleWorldPositionPbData.FromPb(message.Position);
        var newerProfile = profile.Revision > _profile!.Revision;
        var newerStatus = status.Revision > _status!.Revision;
        var newerPosition = position.Revision > _position!.Revision &&
            position.AcknowledgedAction >= _position.AcknowledgedAction;
        if (!newerProfile && !newerStatus && !newerPosition) return;
        Accept(newerProfile ? profile : _profile, newerStatus ? status : _status,
            newerPosition ? position : _position, _role!);
    }

    /// <summary>只合并较新的当前角色资料。</summary>
    private void ApplyProfile(RoleWorldProfilePb message)
    {
        if (_state is null || message.RoleId != RoleId) return;
        var profile = RoleWorldProfilePbData.FromPb(message);
        if (profile.Revision > _profile!.Revision) Accept(profile, _status!, _position!, _role!);
    }

    /// <summary>只合并较新的当前角色位置。</summary>
    private void ApplyPosition(RoleWorldPositionPb message)
    {
        if (_state is null || message.RoleId != RoleId) return;
        var position = RoleWorldPositionPbData.FromPb(message);
        if (position.AcknowledgedAction < _position!.AcknowledgedAction) return;
        if (position.Revision > _position!.Revision) Accept(_profile!, _status!, position, _role!);
    }

    /// <summary>只合并较新的可见状态。</summary>
    private void ApplyStatus(RoleWorldStatusPb message)
    {
        if (_state is null || message.RoleId != RoleId) return;
        var status = RoleWorldStatusPbData.FromPb(message);
        if (status.Revision > _status!.Revision) Accept(_profile!, status, _position!, _role!);
    }

    /// <summary>只合并较新的当前玩家私有角色数据。</summary>
    private void ApplyRole(RolePb message)
    {
        if (_state is null || message.RoleId != RoleId) return;
        var role = RolePbData.FromPb(message);
        if (role.Revision > _role!.Revision) Accept(_profile!, _status!, _position!, role);
    }

    /// <summary>完成跨领域校验后一次替换组合快照。</summary>
    private void Accept(RoleWorldProfilePbData profile, RoleWorldStatusPbData status,
        RoleWorldPositionPbData position, RolePbData role)
    {
        var state = RoleWorldStatePbData.FromParts(profile, status, position, role, Equipment);
        if (state.MapId != _state!.MapId) throw new InvalidDataException("跨地图角色更新尚未开放");
        if (_bag is not null) ConvertBag(_bag.ToPb(), state.BagExtraRows);
        else GameServices.Config.Basic.Value.GetBagCapacity(state.BagExtraRows);
        _profile = profile;
        _status = status;
        _position = position;
        _role = role;
        _state = state;
    }

    /// <summary>完整校验同次提交的两侧状态后原子接纳，不允许公开消息覆盖本人装备。</summary>
    private void OnBagChanged(RoleItemsChangedNotice notice)
    {
        if (_state is null || notice.RoleId != RoleId) return;
        if (notice.Bag is null || notice.Equipment is null)
            throw new InvalidDataException("物品推送缺少背包或装备快照");
        if (_bag is not null && notice.Bag.GetRevision() <= _bag.Revision) return;
        var bag = ConvertBag(notice.Bag, _state.BagExtraRows);
        var equipment = RoleWorldEquipmentPbData.FromPb(notice.Equipment, GameServices.Config.Item.Items);
        if (equipment.RoleId != RoleId || equipment.Revision < Equipment.Revision ||
            (equipment.Revision == Equipment.Revision && !equipment.HasSameItems(Equipment)))
            throw new InvalidDataException("装备身份不符、版本回退或同版本内容冲突");
        equipment.ValidateBag(bag);
        // 信封版本可能增加而展示未变，世界按配置内容判断是否需要刷新。
        var state = RoleWorldStatePbData.FromParts(_profile!, _status!, _position!, _role!, equipment);
        _bag = bag;
        _state = state;
    }

    /// <summary>从统一配置读取规则，构造与协议隔离的完整快照。</summary>
    private static BagPbData ConvertBag(BagPb message, int extraRows) => BagPbData.FromPb(message,
        GameServices.Config.Basic.Value.GetBagCapacity(extraRows), GameServices.Config.Item.Items);

    /// <summary>账号会话释放时清除当前角色数据。</summary>
    protected override void OnDispose() => Reset();
}
