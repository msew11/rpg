using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>处理 protobuf 请求、延迟移动确认及独立服务端持久化。</summary>
public sealed class MockServer
{
    /// <summary>角色 JSON 存储边界。</summary>
    private readonly RepoServerRole _roles;
    /// <summary>按地图身份保存独立规格，不依赖客户端配置服务。</summary>
    private readonly IReadOnlyDictionary<int, DataServerMapRules> _mapRules;
    /// <summary>本服务端已经加载的地图，跨玩家会话保留。</summary>
    private readonly Dictionary<int, ServerMap> _maps = new();
    /// <summary>独立地图文件仓库。</summary>
    private readonly RepoServerMap _mapRepo;
    /// <summary>物品转移的独立文件事务，不拥有业务对象。</summary>
    private readonly RepoServerItemTransfer _transfers;
    /// <summary>尚未共同落地的地图到参与角色关系，角色本版不支持跨图。</summary>
    private readonly Dictionary<int, HashSet<long>> _transferGroups = new();
    /// <summary>仅验收用于模拟提交点前中断。</summary>
    internal RepoServerItemTransfer Transfers => _transfers;
    /// <summary>新角色的初始地图身份。</summary>
    private readonly int _initialMapId;
    /// <summary>当前角色的范围观察者，重置会话时移除。</summary>
    private MapAoiTracker? _aoi;
    /// <summary>由运行环境提供当前检测秒数，隔离测试默认五秒。</summary>
    private readonly Func<int> _flushInterval;
    /// <summary>当前使用的检测周期，用于设置变更后重新计时。</summary>
    private int _lastFlushInterval = -1;
    /// <summary>距离上次检测累计的模拟秒数。</summary>
    private double _flushElapsed;
    /// <summary>启动时复制的独立服务端背包规则。</summary>
    private readonly DataServerBagRules _bagRules;
    /// <summary>当前会话待确认的移动。</summary>
    private readonly List<DataPendingMove> _pending = [];
    /// <summary>已确认的当前角色内存状态。</summary>
    private DataServerRole? _current;
    /// <summary>绑定当前移动的拾取请求，只持有身份及预期版本。</summary>
    private PickupItem? _pickup;
    /// <summary>当前模拟位置，尚未确认为已保存状态。</summary>
    private Vector2 _position;
    /// <summary>最近的移动目标。</summary>
    private Vector2 _target;
    /// <summary>当前允许处理的会话代次。</summary>
    private long _generation;
    /// <summary>是否已通过演示账号验证。</summary>
    private bool _authenticated;
    /// <summary>本会话最近收到的移动序号。</summary>
    private long _lastAction;
    /// <summary>服务器发出的独立 protobuf 信封字节。</summary>
    public event Action<byte[]>? Outgoing;

    /// <summary>绑定独立服务端存储目录。</summary>
    public MockServer(string directory, DataServerBagRules bagRules, Action<string, string>? writeLog = null,
        Func<int>? flushInterval = null, IEnumerable<DataServerMapRules>? maps = null, int initialMapId = 1)
    {
        _transfers = new RepoServerItemTransfer(directory, writeLog);
        _transfers.Recover();
        _roles = new RepoServerRole(directory, writeLog, deferWrites: true);
        _flushInterval = flushInterval ?? (() => 5);
        _bagRules = bagRules;
        _mapRules = (maps ?? [new DataServerMapRules(1, 1000, 1000, 32, 32)]).ToDictionary(map => map.MapId);
        foreach (var map in _mapRules.Values) map.Validate();
        if (!_mapRules.ContainsKey(initialMapId)) throw new ArgumentException("初始地图不存在");
        _initialMapId = initialMapId;
        _mapRepo = new RepoServerMap(directory, writeLog);
    }

    /// <summary>结束角色模拟并更新代次；保留账号登录供返回选角使用。</summary>
    public void ResetSession(long generation)
    {
        _generation = generation;
        _current = null;
        _aoi = null;
        _pending.Clear();
        _pickup = null;
        _lastAction = 0;
    }

    /// <summary>解析客户端字节并将允许的请求交给处理方法。</summary>
    public void Receive(byte[] packet)
    {
        var envelope = MessageCodec.ReadEnvelope(packet);
        if (envelope.Generation != _generation) return;
        switch (MessageCodec.ReadPayload(envelope, true))
        {
            case Login login: HandleLogin(login); break;
            case SelectRole select: HandleSelectRole(select); break;
            case CreateRole create: HandleCreateRole(create); break;
            case DeleteRole delete: HandleDeleteRole(delete); break;
            case Move move: HandleMove(move); break;
            case Gm gm: HandleGm(gm); break;
            case MoveItem moveItem: HandleMoveItem(moveItem); break;
            case DropItem dropItem: HandleDropItem(dropItem); break;
            case PickupItem pickup: HandlePickupItem(pickup); break;
            case ResyncMapAoi resync: HandleResyncMapAoi(resync); break;
        }
    }

    /// <summary>先推进业务内存，再按周期检测落地；无当前角色时也执行检测。</summary>
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        AdvanceMovement(delta);
        AdvancePickup();
        if (_current is not null && _aoi is not null)
        {
            var change = _aoi.Move(GetMap(_current.MapId), _position.X, _position.Y);
            if (change is not null) Send(change);
        }
        var interval = _flushInterval();
        if (interval != _lastFlushInterval)
        {
            _lastFlushInterval = interval;
            _flushElapsed = 0;
        }
        if (interval == 0) return;
        _flushElapsed += delta;
        if (_flushElapsed < interval) return;
        _flushElapsed %= interval;
        Flush();
    }

    /// <summary>在关闭或显式保存节点提交所有待写文件，失败保留数据供重试。</summary>
    public void Flush()
    {
        var errors = new List<Exception>();
        _transfers.Recover();
        var involved = _transferGroups.Values.SelectMany(ids => ids).ToHashSet();
        var groupedMaps = _transferGroups.Keys.ToHashSet();
        foreach (var (mapId, ids) in _transferGroups.ToArray())
        {
            var map = _maps[mapId];
            var logs = new List<(string Path, string Text)>();
            try
            {
                _transfers.Begin(mapId, ids);
                _roles.WritePending(ids, (path, text) => logs.Add((path, text)));
                logs.AddRange(map.WritePending());
                _transfers.Complete();
            }
            catch (Exception error)
            {
                errors.Add(error);
                try { _transfers.Recover(); } catch (Exception recovery) { errors.Add(recovery); }
                continue;
            }
            _roles.AcceptPending(ids);
            map.AcceptPending();
            _transferGroups.Remove(mapId);
            _transfers.LogCommitted(logs);
            try { map.Flush(); } catch (Exception cleanup) { errors.Add(cleanup); }
        }
        try { _roles.Flush(involved); } catch (Exception error) { errors.Add(error); }
        foreach (var (mapId, map) in _maps)
            if (!groupedMaps.Contains(mapId))
                try { map.Flush(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("服务端落地失败", errors);
    }

    /// <summary>按身份按需加载一张地图，完整校验后才发布到服务端集合。</summary>
    internal ServerMap GetMap(int mapId)
    {
        if (!_mapRules.TryGetValue(mapId, out var rules)) throw new InvalidOperationException("地图不存在");
        if (_maps.TryGetValue(mapId, out var existing)) return existing;
        var map = new ServerMap(rules, _mapRepo, _bagRules.Items.ToDictionary(item => item.Key, item => item.Value.MaxStack));
        _maps.Add(mapId, map);
        return map;
    }

    /// <summary>创建权威地图对象并交付当前观察者可见的增量。</summary>
    public DataServerMapObject CreateMapObject(int mapId, DataServerMapObject value)
    {
        var map = GetMap(mapId);
        var change = map.Create(value);
        PublishMapChange(map, change);
        return change.After!;
    }

    /// <summary>更新权威对象，地图边界及预期版本由地图校验。</summary>
    public DataServerMapObject UpdateMapObject(int mapId, DataServerMapObject value)
    {
        var map = GetMap(mapId);
        var change = map.Update(value);
        PublishMapChange(map, change);
        return change.After!;
    }

    /// <summary>删除权威对象，离开 AOI 本身不会调用此接口。</summary>
    public void RemoveMapObject(int mapId, Guid id)
    {
        var map = GetMap(mapId);
        PublishMapChange(map, map.Remove(id));
    }

    /// <summary>地图通过返回值交付修改，不反向调用通信层。</summary>
    private void PublishMapChange(ServerMap map, DataServerMapChange change)
    {
        if (change.Before == change.After || _current?.MapId != map.Rules.MapId || _aoi is null) return;
        var notice = _aoi.Change(map, change);
        if (notice is not null) Send(notice);
    }

    /// <summary>重同步只能读取当前角色的权威范围，不能申请其他地图。</summary>
    private void HandleResyncMapAoi(ResyncMapAoi request)
    {
        var response = new ResyncMapAoiRt { RoleId = request.RoleId, MapId = request.MapId };
        try
        {
            if (_current is null || _aoi is null || request.RoleId != _current.RoleId || request.MapId != _current.MapId)
                throw new InvalidOperationException("重同步角色或地图身份无效");
            response.Snapshot = _aoi.Snapshot(GetMap(_current.MapId), _position.X, _position.Y);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        Send(response);
    }

    /// <summary>按既有模拟速度推进位置，延迟 0.45 秒后提交内存并返回移动确认。</summary>
    private void AdvanceMovement(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (_current is null) return;
        var distance = Vector2.Distance(_position, _target);
        var step = (float)(180 * delta);
        _position = distance <= step || distance == 0 ? _target : _position + (_target - _position) / distance * step;
        for (var index = 0; index < _pending.Count;)
        {
            var pending = _pending[index] with { Remaining = _pending[index].Remaining - delta };
            if (pending.Remaining > 0) { _pending[index++] = pending; continue; }
            DataServerRole candidate;
            try
            {
                candidate = _current with { X = Math.Clamp(_position.X + 3, 0, (long)_mapRules[_current.MapId].Width * _mapRules[_current.MapId].TileSize - 1), Y = _position.Y,
                    Revision = checked(_current.Revision + 1), AcknowledgedAction = pending.ActionId };
                _roles.SaveRole(candidate);
            }
            catch (Exception error)
            {
                _position = _target = new Vector2(_current.X, _current.Y);
                var failed = _pending.ToArray();
                _pending.Clear();
                _pickup = null;
                foreach (var move in failed) Send(new MoveRt { RoleId = _current.RoleId, ActionId = move.ActionId,
                    Error = $"角色存档保存失败：{error.Message}" });
                return;
            }
            _current = candidate;
            _pending.RemoveAt(index);
            Send(new MoveRt { Success = true, RoleId = candidate.RoleId, ActionId = pending.ActionId, State = ToMessage(candidate) });
        }
    }

    /// <summary>主动提交当前角色的新状态，内存提交成功后发推送；失败由调用方观察异常。</summary>
    public void PublishRoleState(DataServerRole update)
    {
        if (_current is null || update.RoleId != _current.RoleId) throw new InvalidOperationException("只能推送当前角色状态");
        var candidate = update with { Revision = checked(_current.Revision + 1), AcknowledgedAction = _current.AcknowledgedAction };
        _bagRules.Validate(candidate);
        if (candidate.MapId != _current.MapId || !GetMap(candidate.MapId).Rules.Contains(candidate.X, candidate.Y))
            throw new InvalidOperationException("角色地图或位置无效，跨地图迁移尚未开放");
        _roles.Save(candidate);
        _current = candidate;
        _position = _target = new Vector2(candidate.X, candidate.Y);
        Send(new RoleStateNotice { State = ToMessage(candidate) });
        var notice = _aoi?.Move(GetMap(candidate.MapId), _position.X, _position.Y);
        if (notice is not null) Send(notice);
    }

    /// <summary>验证演示账号并从服务端缓存读取独立角色摘要。</summary>
    private void HandleLogin(Login request)
    {
        _authenticated = false;
        _current = null;
        _aoi = null;
        _pending.Clear();
        _pickup = null;
        if (request.Username != "demo" || request.Password != "demo")
        {
            Send(new LoginRt { Error = "用户名或密码错误" });
            return;
        }
        var response = new LoginRt();
        try
        {
            foreach (var role in _roles.List()) response.Roles.Add(ToSummary(role));
            response.Success = _authenticated = true;
        }
        catch (Exception error) { response.Error = error.Message; response.Roles.Clear(); }
        Send(response);
    }

    /// <summary>加载选中角色并重置本次会话的移动序号。</summary>
    private void HandleSelectRole(SelectRole request)
    {
        var response = new SelectRoleRt { RoleId = request.RoleId };
        MapAoiSnapshotPb? snapshot = null;
        try
        {
            if (!_authenticated) throw new InvalidOperationException("请先登录");
            var role = _roles.Load(request.RoleId) with { AcknowledgedAction = 0 };
            _bagRules.Validate(role);
            var map = GetMap(role.MapId);
            if (!map.Rules.Contains(role.X, role.Y)) throw new InvalidOperationException("角色位置超出所属地图");
            var observer = new MapAoiTracker(role.RoleId, role.MapId);
            snapshot = observer.Snapshot(map, role.X, role.Y);
            _pending.Clear();
            _pickup = null;
            _lastAction = 0;
            _position = _target = new Vector2(role.X, role.Y);
            _current = role;
            _aoi = observer;
            response.Success = true;
            response.State = ToMessage(role);
            response.Bag = ToBagMessage(role.Bag);
        }
        catch (Exception error) { response.Error = error.Message; }
        Send(response);
        if (response.Success && snapshot is not null) Send(new MapAoiNotice
        { RoleId = request.RoleId, MapId = snapshot.MapId, Sequence = snapshot.Sequence, Snapshot = snapshot });
    }

    /// <summary>认证后在角色列表创建角色，成功列表在写入前完整准备。</summary>
    private void HandleCreateRole(CreateRole request)
    {
        var response = new CreateRoleRt();
        try
        {
            EnsureRoleList();
            var summaries = new List<RoleSummaryPb>();
            foreach (var existing in _roles.List()) summaries.Add(ToSummary(existing));
            var gender = request.Gender switch
            {
                RoleGenderPb.Male => ServerRoleGender.Male,
                RoleGenderPb.Female => ServerRoleGender.Female,
                _ => throw new ArgumentException("角色性别无效")
            };
            var rules = _mapRules[_initialMapId];
            var role = _roles.Create(request.Name, gender, request.HeadId, _initialMapId,
                Math.Min(gender == ServerRoleGender.Male ? 16016 : 16144, (long)rules.Width * rules.TileSize - 1),
                Math.Min(gender == ServerRoleGender.Male ? 16016 : 16080, (long)rules.Height * rules.TileSize - 1));
            summaries.Add(ToSummary(role));
            response.Roles.AddRange(summaries);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        Send(response);
    }

    /// <summary>缓存墓碑后返回已准备的剩余列表。</summary>
    private void HandleDeleteRole(DeleteRole request)
    {
        var response = new DeleteRoleRt();
        try
        {
            EnsureRoleList();
            var summaries = new List<RoleSummaryPb>();
            foreach (var role in _roles.List())
                if (role.RoleId != request.RoleId) summaries.Add(ToSummary(role));
            _roles.Delete(request.RoleId);
            response.Roles.AddRange(summaries);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        Send(response);
    }

    /// <summary>服务端独立验证认证及当前角色状态，不信任客户端页面。</summary>
    private void EnsureRoleList()
    {
        if (!_authenticated || _current is not null) throw new InvalidOperationException("请在登录后的角色列表操作");
    }

    /// <summary>验证角色、坐标及序号后排队，成功确认由时间推进产生。</summary>
    private void HandleMove(Move request)
    {
        if (_current is null || request.RoleId != _current.RoleId || request.ActionId <= _lastAction ||
            !_mapRules[_current.MapId].Contains(request.X, request.Y))
        {
            Send(new MoveRt { RoleId = request.RoleId, ActionId = request.ActionId, Error = "无效的角色、坐标或移动序号" });
            return;
        }
        if (_pickup is { } canceled)
        {
            _pickup = null;
            Send(PickupResponse(canceled, "拾取已取消"));
        }
        _lastAction = request.ActionId;
        _target = new Vector2(request.X, request.Y);
        _pending.Add(new DataPendingMove(request.ActionId, 0.45));
    }

    /// <summary>解析 GM 并在候选副本上发放，缓存提交成功后反馈。</summary>
    private void HandleGm(Gm request)
    {
        var response = new GmRt { RoleId = request.RoleId, Command = request.Command };
        DataServerRole candidate;
        try
        {
            if (!_authenticated || _current is null || request.RoleId != _current.RoleId)
                throw new InvalidOperationException("请先进入当前角色");
            var args = request.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0 || args[0] != "item") throw new ArgumentException("未知 GM 指令；用法：/item <道具ID> [数量]");
            if (args.Length is < 2 or > 3 ||
                !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId <= 0)
                throw new ArgumentException("用法：/item <正整数道具ID> [正整数数量]");
            var count = 1;
            if (args.Length == 3 && (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0))
                throw new ArgumentException("道具数量必须是有效的正整数");
            if (!_bagRules.Items.TryGetValue(itemId, out var definition)) throw new ArgumentException($"道具配置不存在：{itemId}");
            candidate = _bagRules.Add(_current, itemId, count);
            _roles.SaveBag(candidate);
            response.Success = true;
            response.Message = $"已发放 {definition.Name}（{itemId}）× {count}";
        }
        catch (Exception error)
        {
            response.Message = error.Message;
            Send(response);
            return;
        }
        _current = candidate;
        Send(new BagChangedNotice { RoleId = candidate.RoleId, Bag = ToBagMessage(candidate.Bag) });
        Send(response);
    }

    /// <summary>在独立候选背包内移动、合并或交换，原子保存成功后才发布快照。</summary>
    private void HandleMoveItem(MoveItem request)
    {
        var response = new MoveItemRt { RoleId = request.RoleId, InstanceId = request.InstanceId,
            TargetSlotIndex = request.TargetSlotIndex };
        DataServerRole candidate;
        try
        {
            if (!_authenticated || _current is null || request.RoleId != _current.RoleId)
                throw new InvalidOperationException("请先进入当前角色");
            if (request.ExpectedRevision != _current.Bag.Revision)
                throw new InvalidOperationException("背包已更新，请重新拖动物品");
            _bagRules.Validate(_current);
            if (request.TargetSlotIndex < 0 || request.TargetSlotIndex >= _bagRules.GetCapacity(_current.BagExtraRows))
                throw new ArgumentException("目标格子超出背包范围");
            var source = _current.Bag.Items.FirstOrDefault(item => item.InstanceId == request.InstanceId)
                ?? throw new ArgumentException("源物品不存在");
            if (source.SlotIndex == request.TargetSlotIndex)
            {
                response.Success = true;
                Send(response);
                return;
            }
            var target = _current.Bag.Items.FirstOrDefault(item => item.SlotIndex == request.TargetSlotIndex);
            var items = _current.Bag.Items.ToList();
            items.Remove(source);
            if (target is null) items.Add(source with { SlotIndex = request.TargetSlotIndex });
            else
            {
                items.Remove(target);
                var maxStack = _bagRules.Items[source.ItemId].MaxStack;
                if (source.ItemId == target.ItemId && target.Count < maxStack)
                {
                    var transferred = Math.Min(source.Count, maxStack - target.Count);
                    items.Add(target with { Count = target.Count + transferred });
                    if (source.Count > transferred) items.Add(source with { Count = source.Count - transferred });
                }
                else
                {
                    items.Add(source with { SlotIndex = target.SlotIndex });
                    items.Add(target with { SlotIndex = source.SlotIndex });
                }
            }
            candidate = _current with { Bag = _current.Bag with
            {
                Revision = checked(_current.Bag.Revision + 1),
                Items = items.OrderBy(item => item.SlotIndex).ToArray()
            } };
            _bagRules.Validate(candidate);
            _roles.SaveBag(candidate);
        }
        catch (Exception error)
        {
            response.Error = error.Message;
            Send(response);
            return;
        }
        _current = candidate;
        Send(new BagChangedNotice { RoleId = candidate.RoleId, Bag = ToBagMessage(candidate.Bag) });
        response.Success = true;
        Send(response);
    }

    /// <summary>完整校验后丢弃或销毁物品；仅普通丢弃创建地图对象和共同落地关系。</summary>
    private void HandleDropItem(DropItem request)
    {
        var response = new DropItemRt { RoleId = request.RoleId, InstanceId = request.InstanceId };
        DataServerRole candidate;
        ServerMap? map = null;
        DataServerMapChange? change = null;
        try
        {
            if (!_authenticated || _current is null || request.RoleId != _current.RoleId)
                throw new InvalidOperationException("请先进入当前角色");
            if (request.ExpectedRevision != _current.Bag.Revision)
                throw new InvalidOperationException("背包已更新，请重新选择丢弃物品");
            _bagRules.Validate(_current);
            var source = _current.Bag.Items.SingleOrDefault(item => item.InstanceId == request.InstanceId)
                ?? throw new ArgumentException("源物品不存在");
            if (request.Count <= 0 || request.Count > source.Count) throw new ArgumentException("丢弃数量无效");
            candidate = _current with { Bag = _current.Bag with
            {
                Revision = checked(_current.Bag.Revision + 1),
                Items = _current.Bag.Items.Where(item => item.InstanceId != source.InstanceId)
                    .Concat(request.Count == source.Count ? [] : new[] { source with { Count = source.Count - request.Count } })
                    .OrderBy(item => item.SlotIndex).ToArray()
            } };
            _bagRules.Validate(candidate);
            if (!request.Destroy)
            {
                map = GetMap(_current.MapId);
                if (request.HasDropX != request.HasDropY) throw new ArgumentException("丢弃落点坐标不完整");
                var point = request.HasDropX && request.HasDropY
                    ? ValidateGroundDropPosition(request, map.Rules) : GroundDropPosition(map.Rules);
                var ground = new DataServerMapObject { X = point.X, Y = point.Y, ItemId = source.ItemId, Count = request.Count };
                // 创建地面物品后绑定共同落地；销毁不产生新的地图变更关系。
                change = map.Create(ground);
                if (!_transferGroups.TryGetValue(candidate.MapId, out var ids)) _transferGroups[candidate.MapId] = ids = new();
                ids.Add(candidate.RoleId);
                response.ObjectId = ground.ObjectId.ToString("N");
            }
            _roles.SaveBag(candidate);
        }
        catch (Exception error)
        {
            response.Error = error.Message;
            Send(response);
            return;
        }
        _current = candidate;
        response.Success = true;
        Send(new BagChangedNotice { RoleId = candidate.RoleId, Bag = ToBagMessage(candidate.Bag) });
        if (map is not null && change is not null) PublishMapChange(map, change);
        Send(response);
    }

    /// <summary>只验证客户端落点与权威角色位置、身体避让区及地图的关系。</summary>
    private Vector2 ValidateGroundDropPosition(DropItem request, DataServerMapRules rules)
    {
        var point = new Vector2(request.DropX, request.DropY);
        if (!rules.Contains(point.X, point.Y)) throw new ArgumentException("丢弃落点超出地图");
        var delta = point - _position;
        var screenX = (delta.X - delta.Y) * 0.5f;
        var screenY = (delta.X + delta.Y) * 0.25f;
        var radiusSquared = screenX * screenX + screenY * screenY;
        // 移动确认的 3 逻辑像素修正及一帧同步差容许约 2 屏幕像素误差。
        if (!float.IsFinite(radiusSquared) || radiusSquared > 58f * 58f ||
            radiusSquared < 46f * 46f || (MathF.Abs(screenX) < 38f && screenY < 22f))
            throw new ArgumentException("丢弃落点不在角色周围的有效区域");
        return point;
    }

    /// <summary>兼容旧请求，在等轴画面周围选点；狭小地图使用裁剪后最远候选。</summary>
    private Vector2 GroundDropPosition(DataServerMapRules rules)
    {
        var candidates = new List<Vector2>();
        var farthest = _position;
        var farthestDistance = -1f;
        for (var index = 0; index < 64; index++)
        {
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var radius = 48 + Random.Shared.NextSingle() * 8;
            var screen = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var point = _position + new Vector2(screen.X + 2 * screen.Y, 2 * screen.Y - screen.X);
            var clamped = Vector2.Clamp(point, Vector2.Zero,
                new Vector2((long)rules.Width * rules.TileSize - 1, (long)rules.Height * rules.TileSize - 1));
            var delta = clamped - _position;
            var projected = new Vector2((delta.X - delta.Y) * 0.5f, (delta.X + delta.Y) * 0.25f);
            if (projected.LengthSquared() > farthestDistance)
            {
                farthestDistance = projected.LengthSquared();
                farthest = clamped;
            }
            if (rules.Contains(point.X, point.Y) && (MathF.Abs(screen.X) >= 40 || screen.Y >= 24))
                candidates.Add(point);
        }
        return candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : farthest;
    }

    /// <summary>只接纳当前 AOI 对象及匹配的走近目标，远处暂存意图，近处立即尝试。</summary>
    private void HandlePickupItem(PickupItem request)
    {
        try
        {
            _ = ValidatePickup(request);
            if (_pickup is not null)
            {
                if (_pickup.Equals(request)) return;
                throw new InvalidOperationException("已有待完成的拾取");
            }
        }
        catch (Exception error) { Send(PickupResponse(request, error.Message)); return; }
        _pickup = request.Clone();
        AdvancePickup();
    }

    /// <summary>校验身份、对象和背包版本，以及服务端当前位置的九块范围与移动绑定。</summary>
    private DataServerMapObject ValidatePickup(PickupItem request)
    {
        if (!_authenticated || _current is null || request.RoleId != _current.RoleId || request.MoveActionId <= 0 || request.MoveActionId != _lastAction)
            throw new InvalidOperationException("拾取角色或移动意图无效");
        if (request.ExpectedBagRevision != _current.Bag.Revision) throw new InvalidOperationException("背包已更新，请重新拾取");
        var map = GetMap(_current.MapId);
        if (!Guid.TryParseExact(request.ObjectId, "N", out var id) || !map.Objects.TryGetValue(id, out var item) || item.MonsterTypeId is not null)
            throw new InvalidOperationException("地面物品已不存在");
        if (request.ExpectedObjectRevision != item.Revision) throw new InvalidOperationException("地面物品已变化，请重新拾取");
        var center = map.Rules.ChunkAt(_position.X, _position.Y);
        if (!map.Rules.Neighborhood(center.X, center.Y).Contains(map.Rules.ChunkAt(item.X, item.Y)) ||
            Vector2.DistanceSquared(_target, new Vector2(item.X, item.Y)) > 0.01f)
            throw new InvalidOperationException("物品不在当前范围或走近目标不匹配");
        return item;
    }

    /// <summary>移动推进后按权威距离整份入包，复用现有转移关系与文件提交。</summary>
    private void AdvancePickup()
    {
        if (_pickup is not { } request) return;
        DataServerRole candidate;
        ServerMap map;
        DataServerMapChange change;
        try
        {
            var item = ValidatePickup(request);
            map = GetMap(_current!.MapId);
            if (Vector2.DistanceSquared(_position, new Vector2(item.X, item.Y)) > (double)map.Rules.TileSize * map.Rules.TileSize) return;
            candidate = _bagRules.Add(_current, item.ItemId, item.Count);
            RepoServerRole.Validate(candidate);
            change = map.Remove(item.ObjectId);
            _roles.SaveBag(candidate);
            if (!_transferGroups.TryGetValue(candidate.MapId, out var ids)) _transferGroups[candidate.MapId] = ids = new();
            ids.Add(candidate.RoleId);
        }
        catch (Exception error)
        {
            _pickup = null;
            Send(PickupResponse(request, error.Message));
            return;
        }
        _pickup = null;
        _current = candidate;
        Send(new BagChangedNotice { RoleId = candidate.RoleId, Bag = ToBagMessage(candidate.Bag) });
        PublishMapChange(map, change);
        Send(PickupResponse(request));
    }

    /// <summary>最终结果只携带关联身份，两侧缓存走已有独立通知。</summary>
    private static PickupItemRt PickupResponse(PickupItem request, string error = "") => new()
    {
        RoleId = request.RoleId, ObjectId = request.ObjectId, MoveActionId = request.MoveActionId,
        Success = error.Length == 0, Error = error
    };

    /// <summary>复制服务端实例为独立完整背包协议。</summary>
    private static BagPb ToBagMessage(DataServerBag bag)
    {
        var message = new BagPb { Revision = bag.Revision };
        message.Items.AddRange(bag.Items.Select(item => new ItemPb
        {
            InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = item.SlotIndex, Count = item.Count
        }));
        return message;
    }

    /// <summary>登录只返回选角页所需的角色摘要。</summary>
    private static RoleSummaryPb ToSummary(DataServerRole role) => new()
    {
        RoleId = role.RoleId, Name = role.Name, Level = role.Level, HeadId = role.HeadId,
        Gender = role.Gender == ServerRoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female
    };

    /// <summary>将完整角色世界状态复制为协议对象，不向客户端共享服务端模型。</summary>
    private static RoleWorldStatePb ToMessage(DataServerRole role) => new()
    {
        RoleId = role.RoleId, MapId = role.MapId, Name = role.Name, Level = role.Level, HeadId = role.HeadId, Health = role.Health, MaxHealth = role.MaxHealth,
        Mana = role.Mana, MaxMana = role.MaxMana, Experience = role.Experience, NextExperience = role.NextExperience,
        X = role.X, Y = role.Y, Gender = role.Gender == ServerRoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female,
            Revision = role.Revision, AcknowledgedAction = role.AcknowledgedAction, BagExtraRows = role.BagExtraRows
    };

    /// <summary>冻结响应为当前代次的字节交给传输层。</summary>
    private void Send(IMessage response) => Outgoing?.Invoke(MessageCodec.Encode(response, _generation, false));

    /// <summary>记录等待延迟确认的移动。</summary>
    /// <param name="ActionId">客户端提交的意图序号。</param>
    /// <param name="Remaining">距离确认剩余的模拟秒数。</param>
    private sealed record DataPendingMove(long ActionId, double Remaining);
}
