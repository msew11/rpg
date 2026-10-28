using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Rpg.Config;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server.Actors;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Context;
using Rpg.Server.Handler;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Map;

namespace Rpg.Server;

/// <summary>本地客户端字节边界、分片目录和落地协调；业务回合全部进入 Actor 邮箱。</summary>
public sealed class MockServer : IDisposable
{
    /// <summary>客户端调用边界，保证发包、重置与落地的顺序。</summary>
    private readonly object _boundary = new();
    /// <summary>按角色身份的唯一分片。</summary>
    private readonly ConcurrentDictionary<long, RoleActor> _roles = new();
    /// <summary>按地图身份的唯一分片。</summary>
    private readonly ConcurrentDictionary<int, WorldActor> _worlds = new();
    /// <summary>启动默认创建的非分片登录 Actor。</summary>
    private readonly LoginActor _login;
    /// <summary>只负责文件读写的角色仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>地图文件仓库。</summary>
    private readonly RepoServerMap _mapRepo;
    /// <summary>角色地图共同文件事务。</summary>
    private readonly RepoServerItemTransfer _transfers;
    /// <summary>验收模拟共同提交中断。</summary>
    internal RepoServerItemTransfer Transfers => _transfers;
    /// <summary>不可变业务规则。</summary>
    private readonly DataServerRules _rules;
    /// <summary>当前检测秒数。</summary>
    private readonly Func<int> _flushInterval;
    /// <summary>当前模拟移动速度。</summary>
    private readonly Func<int> _movementSpeed;
    /// <summary>最近检测间隔。</summary>
    private int _lastFlushInterval = -1;
    /// <summary>周期累计秒数。</summary>
    private double _flushElapsed;
    /// <summary>客户端会话代次。</summary>
    private long _generation;
    /// <summary>唯一发送序号。</summary>
    private long _revision;
    /// <summary>当前客户端通过认证。</summary>
    private bool _authenticated;
    /// <summary>本连接选择的角色身份。</summary>
    private long? _selected;
    /// <summary>全部 Actor 已停止。</summary>
    private bool _disposed;
    /// <summary>独立信封字节，由同步客户端边界交付。</summary>
    public event Action<byte[]>? Outgoing;

    /// <summary>建立存储边界及默认登录 Actor，角色和地图保持按需创建。</summary>
    public MockServer(string directory, BagRules bagRules, Action<string, string>? writeLog = null,
        Func<int>? flushInterval = null, IEnumerable<MapLayout>? maps = null, int initialMapId = 1,
        Func<int>? movementSpeed = null)
    {
        _rules = new DataServerRules(bagRules, maps, initialMapId);
        _transfers = new(directory, writeLog);
        _transfers.Recover();
        _repo = new(directory, writeLog);
        _mapRepo = new(directory, writeLog);
        _login = new(_repo, _rules);
        _login.Tell(new DataInitialize());
        _flushInterval = flushInterval ?? (() => 5);
        _movementSpeed = movementSpeed ?? (() => EnvService.DefaultMovementSpeed);
    }

    /// <summary>重置连接及当前角色模拟，保留全部已加载分片和待写结果。</summary>
    public void ResetSession(long generation)
    {
        lock (_boundary)
        {
            ThrowIfStopped();
            if (_selected is { } id) Work(id, cache => { cache.Session.Reset(); return true; });
            _selected = null;
            _generation = generation;
        }
    }

    /// <summary>验证客户端会话后，按消息身份进入对应邮箱。</summary>
    public void Receive(byte[] packet)
    {
        lock (_boundary)
        {
            ThrowIfStopped();
            var envelope = MessageCodec.ReadEnvelope(packet);
            if (envelope.Generation != _generation) return;
            var request = MessageCodec.ReadPayload(envelope, true);
            try
            {
                if (request is Login or CreateRole or DeleteRole)
                {
                    if (request is Login)
                    {
                        if (_selected is { } previous) Work(previous, cache => { cache.Session.Reset(); return true; });
                        _selected = null;
                        _authenticated = false;
                    }
                    // 已加载角色删除前把其最新单例移交登录回合；物品表无需再次读取。
                    if (request is DeleteRole deletion && _authenticated && _selected is null &&
                        _roles.TryGetValue(deletion.RoleId, out var loaded) && loaded.State == ActorState.RUN)
                    {
                        var role = (RoleEntityTables)Work(deletion.RoleId, cache => cache.EntityCache.Tables!).Response!;
                        if (!role.Role.Value.IsDeleted)
                            _login.Ask<bool>(new DataLoginWork(cache => { cache.EntityCache.Deletions[deletion.RoleId] = role.Role.Value; return true; })).GetAwaiter().GetResult();
                    }
                    var reply = _login.Ask<IMessage>(new DataLoginRequest(request, _authenticated, _selected.HasValue)).GetAwaiter().GetResult();
                    if (reply is LoginRt login) _authenticated = login.Success;
                    if (request is DeleteRole deleted && reply is DeleteRoleRt { Success: true } &&
                        _roles.TryGetValue(deleted.RoleId, out var existing) && existing.State == ActorState.RUN)
                    {
                        Work(deleted.RoleId, cache => { cache.Commit(cache.EntityCache.Tables!.WithRole(cache.EntityCache.Tables.Role.Value with { IsDeleted = true }), 1); return true; });
                        _login.Ask<bool>(new DataLoginWork(cache => { cache.EntityCache.Deletions.Remove(deleted.RoleId); return true; })).GetAwaiter().GetResult();
                    }
                    Send(reply);
                    return;
                }
                var roleId = RoleId(request);
                if (!_authenticated || request is not SelectRole && _selected != roleId)
                    throw new InvalidOperationException("请先进入当前角色");
                var result = GetRoleActor(roleId).Ask<DataRoleResult>(new DataRoleRequest(roleId, request)).GetAwaiter().GetResult();
                if (result.Response is SelectRoleRt { Success: true }) _selected = roleId;
                Deliver(result);
            }
            catch (Exception error) { Send(Failure(request, error.Message)); }
        }
    }

    /// <summary>先完成角色模拟，再根据统一周期协调各分片落地。</summary>
    public void Tick(double delta)
    {
        lock (_boundary)
        {
            ThrowIfStopped();
            if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (_selected is { } id) Deliver(WorldWork(id, cache => { ServerSimulation.Advance(cache, delta, _movementSpeed()); return true; }));
            var interval = _flushInterval();
            if (interval != _lastFlushInterval) { _lastFlushInterval = interval; _flushElapsed = 0; }
            if (interval == 0) return;
            _flushElapsed += delta;
            if (_flushElapsed < interval) return;
            _flushElapsed %= interval;
            Flush();
        }
    }

    /// <summary>共同事务先准备全部参与分片，提交点成功后才清理各自 dirty。</summary>
    public void Flush()
    {
        lock (_boundary)
        {
            ThrowIfStopped();
            var errors = new List<Exception>();
            _transfers.Recover();
            var involved = new HashSet<long>();
            foreach (var (mapId, world) in _worlds)
            {
                if (world.State == ActorState.STOP) continue;
                var ids = MapAsk<long[]>(mapId, cache => cache.Transfers.ToArray());
                if (ids.Length == 0) { try { MapAsk<bool>(mapId, cache => { cache.Map.Flush(); return true; }); } catch (Exception error) { errors.Add(error); } continue; }
                involved.UnionWith(ids);
                var logs = new List<(string Path, string Text)>();
                try
                {
                    _transfers.Begin(mapId, ids);
                    foreach (var id in ids) WriteRole(id, (path, content) => logs.Add((path, content)), false);
                    logs.AddRange(MapAsk<IReadOnlyList<(string Path, string Text)>>(mapId, cache => cache.Map.WritePending()));
                    _transfers.Complete();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                    try { _transfers.Recover(); } catch (Exception recovery) { errors.Add(recovery); }
                    continue;
                }
                foreach (var id in ids) Work(id, cache => { cache.EntityCache.Dirty = 0; return true; });
                MapAsk<bool>(mapId, cache => { cache.Map.AcceptPending(); cache.Transfers.Clear(); return true; });
                _transfers.LogCommitted(logs);
                try { MapAsk<bool>(mapId, cache => { cache.Map.Flush(); return true; }); } catch (Exception error) { errors.Add(error); }
            }
            foreach (var (id, actor) in _roles)
                if (!involved.Contains(id) && actor.State != ActorState.STOP)
                    try { WriteRole(id, null, true); } catch (Exception error) { errors.Add(error); }
            try
            {
                _login.Ask<bool>(new DataLoginWork(cache =>
                {
                    var failures = new List<Exception>();
                    foreach (var (id, tables) in cache.EntityCache.Created.ToArray())
                        try { _repo.Save(tables); cache.EntityCache.Created.Remove(id); } catch (Exception error) { failures.Add(error); }
                    foreach (var (id, role) in cache.EntityCache.Deletions.ToArray())
                        try { _repo.SaveDeletion(role, cache.EntityCache.Summaries[id]); cache.EntityCache.Deletions.Remove(id); } catch (Exception error) { failures.Add(error); }
                    if (failures.Count != 0) throw new AggregateException(failures);
                    return true;
                })).GetAwaiter().GetResult();
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("服务端落地失败", errors);
        }
    }

    /// <summary>在拥有角色工作副本的邮箱回合中写入，成功提交才确认待写状态。</summary>
    private void WriteRole(long id, Action<string, string>? log, bool accept)
    {
        Work(id, cache =>
        {
            if (cache.EntityCache.Dirty == 0) return true;
            var repo = log is null ? _repo : new RepoServerRole(System.IO.Path.GetDirectoryName(_repo.GetRolePath(id))!, log);
            if (cache.EntityCache.Dirty == 1) repo.SaveRole(cache.EntityCache.Tables!);
            else if (cache.EntityCache.Dirty == 2) repo.SaveBag(cache.EntityCache.Tables!);
            else repo.Save(cache.EntityCache.Tables!);
            if (accept) cache.EntityCache.Dirty = 0;
            return true;
        });
    }

    /// <summary>地图查询返回独立快照，不能从客户端边界直接修改地图。</summary>
    internal DataServerMap GetMap(int mapId) => MapAsk<DataServerMap>(mapId, cache => new DataServerMap(cache.Map.Rules,
        new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, MapObjectEntity>(cache.Map.Objects.ToDictionary(row => row.Key, row => row.Value))));

    /// <summary>通过所属地图 Actor 创建权威对象并同步当前观察者。</summary>
    public MapObjectEntity CreateMapObject(int mapId, MapObjectEntity value) => ChangeMap(mapId, map => map.Create(value)).After!;
    /// <summary>通过所属地图 Actor 更新权威对象。</summary>
    public MapObjectEntity UpdateMapObject(int mapId, MapObjectEntity value) => ChangeMap(mapId, map => map.Update(value)).After!;
    /// <summary>通过所属地图 Actor 删除权威对象。</summary>
    public void RemoveMapObject(int mapId, Guid id) => ChangeMap(mapId, map => map.Remove(id));

    /// <summary>地图修改与观察者更新均进入串行邮箱。</summary>
    private MapChange ChangeMap(int mapId, Func<ServerMap, MapChange> operation)
    {
        lock (_boundary)
        {
            ThrowIfStopped();
            var change = MapAsk<MapChange>(mapId, cache => operation(cache.Map));
            if (_selected is { } id) Deliver(WorldWork(id, cache => { if (cache.Role.EntityCache.Tables!.Role.Value.MapId == mapId) cache.PublishMapChange(cache.GetMap(mapId), change); return true; }));
            return change;
        }
    }

    /// <summary>只建立消息指定身份的角色分片，失败分片可在后续请求重试。</summary>
    internal RoleActor GetRoleActor(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (_roles.TryGetValue(id, out var failed) && failed.State == ActorState.STOP) _roles.TryRemove(id, out _);
        return _roles.GetOrAdd(id, key => new RoleActor(key, _repo, _login, GetWorldActor, _rules));
    }

    /// <summary>以地图身份唯一注册世界分片。</summary>
    internal WorldActor GetWorldActor(int id)
    {
        var layout = _rules.MapLayouts.GetValueOrDefault(id) ?? throw new InvalidOperationException("地图不存在");
        if (_worlds.TryGetValue(id, out var failed) && failed.State == ActorState.STOP) _worlds.TryRemove(id, out _);
        return _worlds.GetOrAdd(id, _ => new WorldActor(layout, _rules, _mapRepo));
    }

    /// <summary>执行带地图身份的 Ask，供协调与内部验收使用。</summary>
    private T MapAsk<T>(int id, Func<ServerWorldCache, object?> work) => GetWorldActor(id).Ask<T>(new DataMapWork(id, work)).GetAwaiter().GetResult();
    /// <summary>执行带角色身份的有序操作。</summary>
    private DataRoleResult Work(long id, Func<RoleContext, object?> work) => GetRoleActor(id).Ask<DataRoleResult>(new DataRoleWork(id, work)).GetAwaiter().GetResult();
    /// <summary>显式移交地图事务，由世界 Actor 串行处理。</summary>
    private DataRoleResult WorldWork(long id, Func<WorldContext, object?> work) => GetRoleActor(id).Ask<DataRoleResult>(new DataRoleWorldWork(id, work)).GetAwaiter().GetResult();
    /// <summary>主动角色操作只允许当前角色，并交付本回合通知。</summary>
    private void RunCurrent(Func<RoleContext, object?> work)
    {
        lock (_boundary) { ThrowIfStopped(); Deliver(Work(_selected ?? throw new InvalidOperationException("请先进入当前角色"), work)); }
    }

    /// <summary>主动地图事务只允许当前角色，并交付本回合通知。</summary>
    private void RunCurrentWorld(Func<WorldContext, object?> work)
    {
        lock (_boundary) { ThrowIfStopped(); Deliver(WorldWork(_selected ?? throw new InvalidOperationException("请先进入当前角色"), work)); }
    }

    /// <summary>外层按既有协议顺序交付本回合通知与响应。</summary>
    private void Deliver(DataRoleResult result)
    {
        if (result.Response is SelectRoleRt select) { Send(select); foreach (var notice in result.Notices) Send(notice); return; }
        foreach (var notice in result.Notices) Send(notice);
        if (result.Response is MoveRt { Success: true } || result.Response is PickupItemRt { Success: true } && result.Role.Session.Pickup is not null) return;
        if (result.Response is IMessage response) Send(response);
    }

    /// <summary>固定协议路由要求角色身份始终来自消息。</summary>
    private static long RoleId(IMessage request) => request switch
    {
        SelectRole value => value.RoleId, Move value => value.RoleId, Gm value => value.RoleId,
        MoveItem value => value.RoleId, EquipItem value => value.RoleId, DropItem value => value.RoleId,
        PickupItem value => value.RoleId, ResyncMapAoi value => value.RoleId,
        _ => throw new InvalidOperationException("未知角色请求")
    };

    /// <summary>初始化和外层校验失败仍按原协议交付错误。</summary>
    private static IMessage Failure(IMessage request, string error) => request switch
    {
        Login => new LoginRt { Error = error }, CreateRole => new CreateRoleRt { Error = error }, DeleteRole => new DeleteRoleRt { Error = error },
        SelectRole value => new SelectRoleRt { RoleId = value.RoleId, Error = error },
        Move value => new MoveRt { RoleId = value.RoleId, ActionId = value.ActionId, Error = error },
        Gm value => new GmRt { RoleId = value.RoleId, Message = error },
        MoveItem value => new MoveItemRt { RoleId = value.RoleId, InstanceId = value.InstanceId, Error = error },
        EquipItem value => new EquipItemRt { RoleId = value.RoleId, Error = error },
        DropItem value => new DropItemRt { RoleId = value.RoleId, InstanceId = value.InstanceId, Error = error },
        PickupItem value => new PickupItemRt { RoleId = value.RoleId, ObjectId = value.ObjectId, MoveActionId = value.MoveActionId, Error = error },
        ResyncMapAoi value => new ResyncMapAoiRt { RoleId = value.RoleId, MapId = value.MapId, Error = error },
        _ => throw new InvalidOperationException(error)
    };

    /// <summary>在唯一发送边界赋予信封序号，先通过邮箱确认物品版本再交付字节。</summary>
    private void Send(IMessage response)
    {
        var revision = checked(++_revision);
        if ((response is RoleItemsChangedNotice || response is SelectRoleRt { Success: true }) && _selected is { } id)
            Work(id, cache => { cache.Session.ItemsRevision = revision; return true; });
        Outgoing?.Invoke(MessageCodec.Encode(response, _generation, false, revision));
    }

    /// <summary>正常关闭先落地，再排空并停止全部独立邮箱。</summary>
    public void Dispose()
    {
        lock (_boundary)
        {
            if (_disposed) return;
            Flush();
            foreach (var actor in _roles.Values) actor.StopAsync().GetAwaiter().GetResult();
            foreach (var actor in _worlds.Values) actor.StopAsync().GetAwaiter().GetResult();
            _login.StopAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }

    /// <summary>释放后拒绝客户端操作。</summary>
    private void ThrowIfStopped() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>提交完整实体候选并发送对应领域推送。</summary>
    public void PublishRoleState(RoleEntityTables update)
    {
        RunCurrentWorld(cache =>
        {
            var previous = cache.Role.RequireRole(update.Role.Value.RoleId);
            var before = previous.Role.Value;
            var role = update.Role.Value;
            cache.Role.Rules.BagRules.Validate(update);
            if (role.MapId != before.MapId || !cache.GetMap(role.MapId).Rules.Contains(role.X, role.Y))
                throw new InvalidOperationException("角色地图或位置无效，跨地图迁移尚未开放");
            var itemsChanged = !previous.Item.Items.OrderBy(row => row.Key).SequenceEqual(update.Item.Items.OrderBy(row => row.Key)) ||
                !previous.Equipment.Items.OrderBy(row => row.Key).SequenceEqual(update.Equipment.Items.OrderBy(row => row.Key));
            cache.Role.Commit(update);
            cache.Role.Session.Position = cache.Role.Session.Target = new Vector2(role.X, role.Y);
            cache.Role.Notices.Enqueue(new RoleStateNotice { State = RoleMessages.World(update, cache.Role.Session.AcknowledgedAction) });
            if (itemsChanged) cache.Role.Notices.Enqueue(RoleMessages.Items(update));
            if (role.Experience != before.Experience || role.NextExperience != before.NextExperience || role.BagExtraRows != before.BagExtraRows)
                cache.Role.Notices.Enqueue(new RoleChangedNotice { Role = RoleMessages.Private(role) });
            PublishAoiPosition(cache);
            return true;
        });
    }

    /// <summary>只提交角色资料并推送。</summary>
    public void PublishRoleProfile(RoleEntity update)
    {
        RunCurrent(cache =>
        {
            var tables = cache.RequireRole(update.RoleId);
            var candidate = tables.WithRole(tables.Role.Value with { Name = update.Name, Level = update.Level, Gender = update.Gender, HeadId = update.HeadId });
            cache.Commit(candidate, 1);
            cache.Notices.Enqueue(new RoleProfileNotice { Profile = RoleMessages.Profile(candidate.Role.Value) });
            return true;
        });
    }
    /// <summary>只提交可见生命魔力并推送。</summary>
    public void PublishRoleStatus(RoleEntity update)
    {
        RunCurrent(cache =>
        {
            var tables = cache.RequireRole(update.RoleId);
            var candidate = tables.WithRole(tables.Role.Value with { Health = update.Health, MaxHealth = update.MaxHealth, Mana = update.Mana, MaxMana = update.MaxMana });
            cache.Commit(candidate, 1);
            cache.Notices.Enqueue(new RoleStatusNotice { Status = RoleMessages.Status(candidate.Role.Value) });
            return true;
        });
    }
    /// <summary>只提交私有经验和扩容并推送。</summary>
    public void PublishRole(RoleEntity update)
    {
        RunCurrent(cache =>
        {
            var tables = cache.RequireRole(update.RoleId);
            var candidate = tables.WithRole(tables.Role.Value with { Experience = update.Experience, NextExperience = update.NextExperience, BagExtraRows = update.BagExtraRows });
            cache.Rules.BagRules.Validate(candidate);
            cache.Commit(candidate);
            cache.Notices.Enqueue(new RoleChangedNotice { Role = RoleMessages.Private(candidate.Role.Value) });
            return true;
        });
    }
    /// <summary>主动提交当前位置并推送，保留资料与物品。</summary>
    public void PublishRolePosition(float x, float y)
    {
        RunCurrentWorld(cache =>
        {
            var tables = cache.Role.Current ?? throw new InvalidOperationException("请先进入当前角色");
            if (!cache.GetMap(tables.Role.Value.MapId).Rules.Contains(x, y)) throw new InvalidOperationException("角色位置无效");
            var candidate = tables.WithRole(tables.Role.Value with { X = x, Y = y });
            cache.Role.Commit(candidate, 1);
            cache.Role.Session.Position = cache.Role.Session.Target = new Vector2(x, y);
            cache.Role.Notices.Enqueue(new RolePositionNotice { Position = RoleMessages.Position(candidate.Role.Value, cache.Role.Session.AcknowledgedAction) });
            PublishAoiPosition(cache);
            return true;
        });
    }
    /// <summary>将当前位置引起的 AOI 差集交给传输。</summary>
    private static void PublishAoiPosition(WorldContext cache)
    {
        if (cache.Role.Current is not { } tables || cache.Role.Session.Aoi is not { } aoi) return;
        var notice = aoi.Move(cache.GetMap(tables.Role.Value.MapId), cache.Role.Session.Position.X, cache.Role.Session.Position.Y);
        if (notice is not null) cache.Role.Notices.Enqueue(notice);
    }
    /// <summary>构造独立公开装备载荷。</summary>
    internal static RoleEquipmentNotice CreateEquipmentNotice(RoleEntityTables tables) => new() { Equipment = RoleMessages.Equipment(tables) };
    /// <summary>公开装备只包含配置编号。</summary>
    internal static RoleWorldEquipmentPb ToEquipmentMessage(RoleEntityTables tables) => RoleMessages.Equipment(tables);
}
