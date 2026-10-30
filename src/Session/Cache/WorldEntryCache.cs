using System;
using System.IO;
using System.Linq;
using Rpg.Services;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>缓存阶段协调完整进入响应，所有候选验证完成前不修改任何业务缓存。</summary>
internal sealed class WorldEntryCache : CacheBase
{
    /// <summary>当前账号摘要与唯一在途状态。</summary>
    private readonly LoginCache _login;
    /// <summary>已接纳角色的缓存引用。</summary>
    private readonly RoleCache _role;
    /// <summary>已接纳地图的缓存引用。</summary>
    private readonly MapCache _map;
    /// <summary>当前唯一请求的角色编号，失败或完成后归零。</summary>
    internal long PendingRoleId { get; private set; }

    /// <summary>由会话组装缓存引用，监听随本账号代次释放。</summary>
    internal WorldEntryCache(LoginCache login, RoleCache role, MapCache map)
    {
        _login = login; _role = role; _map = map;
        Subscribe<global::Rpg.Protocol.SessionKickedNotice>(_ => Cancel());
        Subscribe<global::Rpg.Protocol.EnterWorldRt>(OnEntered);
        Subscribe<global::Rpg.Protocol.ErrorRt>(_ => Cancel());
    }
    /// <summary>发送请求前记录预期身份。</summary>
    internal void Begin(long roleId) => PendingRoleId = roleId;
    /// <summary>失败及页面切换取消等待，保留已接纳的缓存。</summary>
    internal void Cancel() => PendingRoleId = 0;
    /// <summary>完整转换角色、背包、地图及摘要，最后连续提交无失败的赋值。</summary>
    private void OnEntered(global::Rpg.Protocol.EnterWorldRt reply)
    {
        if (PendingRoleId == 0 || !_login.IsLoggedIn || !_login.Expects<global::Rpg.Protocol.EnterWorldRt>()) return;
        try
        {
            var config = GameServices.Config;
            var state = RoleWorldStatePbData.FromPb(reply.State, reply.Role, config.Item.Items);
            if (state.RoleId != PendingRoleId || !_login.Roles.Any(role => role.RoleId == state.RoleId))
                throw new InvalidDataException("进入响应角色身份不一致");
            var bag = BagPbData.FromPb(reply.Bag, config.Basic.Value.GetBagCapacity(state.BagExtraRows), config.Item.Items);
            state.Equipment.ValidateBag(bag);
            var map = DataMapAoiSnapshotPbData.FromPb(reply.Map, state.RoleId);
            if (map.MapId != state.MapId || !config.Map.Items.TryGetValue(map.MapId, out var definition) ||
                map.Layout != new DataMapLayoutPbData(definition.Width, definition.Height, definition.TileSize, definition.ChunkSizeTiles) ||
                map.Layout.ChunkAt(state.Position.X, state.Position.Y) != map.Center)
                throw new InvalidDataException("初始地图、规格或角色位置不一致");
            foreach (var item in map.Objects.Values)
                if (item.MonsterTypeId is null && (!config.Item.Items.TryGetValue(item.ItemId, out var cfg) || item.Count > cfg.MaxStack))
                    throw new InvalidDataException("地图物品配置或数量无效");
            var summary = new RoleSummaryPbData(state.RoleId, state.Name, state.Level, state.Gender, state.HeadId)
                { EquippedItems = state.Equipment.EquippedItems };
            var summaries = Array.AsReadOnly(_login.Roles.Select(role => role.RoleId == state.RoleId ? summary : role).ToArray());
            _role.AcceptEntry(state, bag);
            _map.AcceptEntry(map);
            _login.AcceptEntry(summaries);
        }
        finally { Cancel(); _login.CancelRequest(); }
    }
    /// <summary>账号生命周期结束时丢弃在途身份。</summary>
    protected override void OnDispose() => Cancel();
}
