using System.IO;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>角色会话内唯一的地图 AOI 缓存，不发请求、不修改 ECS。</summary>
public sealed class MapCache : CacheBase
{
    /// <summary>选角请求绑定的角色。</summary>
    private long _roleId;
    /// <summary>选角响应确定的地图。</summary>
    private int _mapId;
    /// <summary>选角时复制的本地地图规格，防止远端更换坐标含义。</summary>
    private DataMapLayoutPbData? _expectedLayout;
    /// <summary>最后完整接纳的不可变快照。</summary>
    public DataMapAoiSnapshotPbData? Snapshot { get; private set; }
    /// <summary>序号缺口已使后续增量暂停。</summary>
    public bool NeedsResync { get; private set; }
    /// <summary>防止每条后续增量重复申请快照。</summary>
    public bool ResyncPending { get; private set; }

    /// <summary>监听由基类托管，保持缓存优先于 UI 的分发阶段。</summary>
    public MapCache()
    {
        Subscribe<SelectRoleRt>(OnSelected);
        Subscribe<MapAoiNotice>(OnNotice);
        Subscribe<ResyncMapAoiRt>(OnResynced);
    }

    /// <summary>切角或离开时清理整份地图状态，保留缓存自身监听。</summary>
    internal void Reset(long roleId = 0)
    {
        _roleId = roleId; _mapId = 0; _expectedLayout = null; Snapshot = null; NeedsResync = false; ResyncPending = false;
    }

    /// <summary>仅在已成功入队后标记在途，由 Session 显式调用。</summary>
    internal void MarkResyncPending() => ResyncPending = true;

    /// <summary>选角成功建立地图身份，不从第一条任意地图通知推断身份。</summary>
    private void OnSelected(SelectRoleRt message)
    {
        if (!message.Success || _roleId == 0 || message.RoleId != _roleId) return;
        if (message.State?.Position is null || message.State.Position.RoleId != _roleId) throw new InvalidDataException("选角缺少地图身份");
        var mapId = message.State.Position.MapId == 0 ? 1 : message.State.Position.MapId;
        if (!GameServices.Config.Map.Items.TryGetValue(mapId, out var config)) throw new InvalidDataException("选角地图无效");
        _expectedLayout = new(config.Width, config.Height, config.TileSize, config.ChunkSizeTiles);
        _mapId = mapId;
    }

    /// <summary>旧身份和序号忽略，缺口暂停，非法批次不部分写缓存。</summary>
    private void OnNotice(MapAoiNotice message)
    {
        if (_mapId == 0 || message.RoleId != _roleId || message.MapId != _mapId) return;
        if (message.Sequence <= (Snapshot?.Sequence ?? 0)) return;
        if (message.UpdateCase == MapAoiNotice.UpdateOneofCase.Snapshot)
        {
            if (message.Snapshot.Sequence != message.Sequence) throw new InvalidDataException("地图快照序号不符");
            ApplySnapshot(message.Snapshot);
            return;
        }
        if (message.UpdateCase != MapAoiNotice.UpdateOneofCase.Delta) throw new InvalidDataException("地图通知缺少更新内容");
        if (NeedsResync || Snapshot is null || message.Sequence != Snapshot.Sequence + 1) { NeedsResync = true; return; }
        var next = Snapshot.Apply(message.Delta, message.Sequence);
        ValidateConfiguration(next);
        Snapshot = next;
    }

    /// <summary>失败保留最后有效状态并报告；成功快照可跨越缺口。</summary>
    private void OnResynced(ResyncMapAoiRt message)
    {
        if (_mapId == 0 || message.RoleId != _roleId || message.MapId != _mapId) return;
        if (!message.Success) { ResyncPending = false; throw new InvalidDataException("地图重同步失败：" + message.Error); }
        if (message.Snapshot is null) throw new InvalidDataException("重同步缺少快照");
        if (message.Snapshot.Sequence <= (Snapshot?.Sequence ?? 0)) return;
        ApplySnapshot(message.Snapshot);
    }

    /// <summary>全部验证后一次替换快照及同步标志。</summary>
    private void ApplySnapshot(MapAoiSnapshotPb value)
    {
        if (value.RoleId != _roleId || value.MapId != _mapId) throw new InvalidDataException("地图快照身份不符");
        var snapshot = DataMapAoiSnapshotPbData.FromPb(value);
        ValidateConfiguration(snapshot);
        Snapshot = snapshot; NeedsResync = false; ResyncPending = false;
    }

    /// <summary>快照与本地已发布配置一致才允许普通阶段消费。</summary>
    private void ValidateConfiguration(DataMapAoiSnapshotPbData snapshot)
    {
        if (snapshot.Layout != _expectedLayout) throw new InvalidDataException("地图同步规格与本地配置不符");
        foreach (var item in snapshot.Objects.Values)
            if (item.MonsterTypeId is null && (!GameServices.Config.Item.Items.TryGetValue(item.ItemId, out var definition) || item.Count > definition.MaxStack))
                throw new InvalidDataException("地面道具配置不存在或数量超限");
    }

    /// <summary>释放后也清除对象引用。</summary>
    protected override void OnDispose() => Reset();
}
