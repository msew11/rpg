using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Rpg.Config;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Handler.Gm;
using Rpg.Server.Handler.Login;
using Rpg.Server.Handler.Role;
using Rpg.Server.Handler.World;
using Rpg.Server.Map;

namespace Rpg.Server;

/// <summary>组织显式加载、请求 Handler、模拟推进、网络发送与旁路落地。</summary>
public sealed class MockServer
{
    /// <summary>角色文件读写边界。</summary>
    private readonly RepoServerRole _roles;
    /// <summary>Handler 共用的纯内存实体入口。</summary>
    private readonly RepoEntityCache _cache;
    /// <summary>地图文件读写边界。</summary>
    private readonly RepoServerMap _mapRepo;
    /// <summary>地图和物品共同文件事务。</summary>
    private readonly RepoServerItemTransfer _transfers;
    /// <summary>验收用于模拟提交点前中断。</summary>
    internal RepoServerItemTransfer Transfers => _transfers;
    /// <summary>当前检测秒数。</summary>
    private readonly Func<int> _flushInterval;
    /// <summary>环境移动速度。</summary>
    private readonly Func<int> _movementSpeed;
    /// <summary>最近使用的检测周期。</summary>
    private int _lastFlushInterval = -1;
    /// <summary>累计检测时间。</summary>
    private double _flushElapsed;
    /// <summary>当前连接代次。</summary>
    private long _generation;
    /// <summary>本发送端非持久化消息序号。</summary>
    private long _revision;
    /// <summary>交给传输的独立信封字节。</summary>
    public event Action<byte[]>? Outgoing;

    /// <summary>绑定 IO 仓库并创建内存入口，业务处理不持有路径或仓库。</summary>
    public MockServer(string directory, BagRules bagRules, Action<string, string>? writeLog = null,
        Func<int>? flushInterval = null, IEnumerable<MapLayout>? maps = null, int initialMapId = 1,
        Func<int>? movementSpeed = null)
    {
        _transfers = new RepoServerItemTransfer(directory, writeLog);
        _transfers.Recover();
        _cache = new RepoEntityCache(bagRules, maps, initialMapId);
        _roles = new RepoServerRole(directory, writeLog, deferWrites: true, cache: _cache);
        _mapRepo = new RepoServerMap(directory, writeLog);
        _flushInterval = flushInterval ?? (() => 5);
        _movementSpeed = movementSpeed ?? (() => EnvService.DefaultMovementSpeed);
    }

    /// <summary>结束角色模拟并更新代次，保留账号和已加载实体。</summary>
    public void ResetSession(long generation)
    {
        _generation = generation;
        _cache.Session.Reset();
        _cache.Notices.Clear();
    }

    /// <summary>显式准备数据后调用对应处理器，集中决定响应发送时机。</summary>
    public void Receive(byte[] packet)
    {
        var envelope = MessageCodec.ReadEnvelope(packet);
        if (envelope.Generation != _generation) return;
        var request = MessageCodec.ReadPayload(envelope, true);
        try { Prepare(request); }
        catch (Exception error)
        {
            Send(request switch
            {
                Login => new LoginRt { Error = error.Message },
                SelectRole select => new SelectRoleRt { RoleId = select.RoleId, Error = error.Message },
                CreateRole => new CreateRoleRt { Error = error.Message },
                DeleteRole => new DeleteRoleRt { Error = error.Message },
                _ => throw new InvalidOperationException("请求加载失败", error)
            });
            return;
        }
        IMessage? response = request switch
        {
            Login login => new LoginHandler().Handle(_cache, login),
            SelectRole select => new SelectRoleHandler().Handle(_cache, select),
            CreateRole create => new CreateRoleHandler().Handle(_cache, create),
            DeleteRole delete => new DeleteRoleHandler().Handle(_cache, delete),
            Move move => new MoveHandler().Handle(_cache, move),
            Gm gm => new GmHandler().Handle(_cache, gm),
            MoveItem item => new MoveItemHandler().Handle(_cache, item),
            EquipItem equip => new EquipItemHandler().Handle(_cache, equip),
            DropItem drop => new DropItemHandler().Handle(_cache, drop),
            PickupItem pickup => new PickupItemHandler().Handle(_cache, pickup),
            ResyncMapAoi resync => new ResyncMapAoiHandler().Handle(_cache, resync),
            _ => null
        };
        if (response is SelectRoleRt) { Send(response); DrainNotices(); return; }
        DrainNotices();
        if (response is MoveRt { Success: true } || response is PickupItemRt { Success: true } && _cache.Session.Pickup is not null) return;
        if (response is not null) Send(response);
    }

    /// <summary>请求处理前执行必要 IO；未认证请求不加载角色文件。</summary>
    private void Prepare(IMessage request)
    {
        switch (request)
        {
            case Login login:
                _cache.Session.Authenticated = false;
                _cache.Session.Reset();
                if (login.Username == "demo" && login.Password == "demo") _roles.ListSummaries();
                break;
            case SelectRole select when _cache.Session.Authenticated:
                var tables = _roles.Load(select.RoleId);
                GetMap(tables.Role.Value.MapId);
                break;
            case CreateRole when _cache.Session.Authenticated && _cache.Current is null:
                _roles.ListSummaries();
                break;
            case DeleteRole delete when _cache.Session.Authenticated && _cache.Current is null:
                _roles.PrepareDelete(delete.RoleId);
                break;
        }
    }

    /// <summary>先推进内存与发送消息，再按周期检测落地。</summary>
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        ServerSimulation.Advance(_cache, delta, _movementSpeed());
        DrainNotices();
        var interval = _flushInterval();
        if (interval != _lastFlushInterval) { _lastFlushInterval = interval; _flushElapsed = 0; }
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
        var involved = _cache.TransferGroups.Values.SelectMany(ids => ids).ToHashSet();
        var groupedMaps = _cache.TransferGroups.Keys.ToHashSet();
        foreach (var (mapId, ids) in _cache.TransferGroups.ToArray())
        {
            var map = _cache.Maps[mapId];
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
            _cache.TransferGroups.Remove(mapId);
            _transfers.LogCommitted(logs);
            try { map.Flush(); } catch (Exception cleanup) { errors.Add(cleanup); }
        }
        try { _roles.Flush(involved); } catch (Exception error) { errors.Add(error); }
        foreach (var (mapId, map) in _cache.Maps)
            if (!groupedMaps.Contains(mapId))
                try { map.Flush(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("服务端落地失败", errors);
    }

    /// <summary>外层显式加载地图，Handler 只能获取已准备实例。</summary>
    internal ServerMap GetMap(int mapId)
    {
        if (!_cache.MapLayouts.TryGetValue(mapId, out var rules)) throw new InvalidOperationException("地图不存在");
        if (_cache.Maps.TryGetValue(mapId, out var existing)) return existing;
        var map = new ServerMap(rules, _mapRepo, _cache.BagRules.Items.ToDictionary(item => item.Key, item => item.Value.MaxStack));
        _cache.Maps.Add(mapId, map);
        return map;
    }

    /// <summary>创建权威对象并发送可见增量。</summary>
    public MapObjectEntity CreateMapObject(int mapId, MapObjectEntity value)
    {
        var map = GetMap(mapId);
        var change = map.Create(value);
        _cache.PublishMapChange(map, change);
        DrainNotices();
        return change.After!;
    }
    /// <summary>更新权威对象并发送可见增量。</summary>
    public MapObjectEntity UpdateMapObject(int mapId, MapObjectEntity value)
    {
        var map = GetMap(mapId);
        var change = map.Update(value);
        _cache.PublishMapChange(map, change);
        DrainNotices();
        return change.After!;
    }
    /// <summary>删除对象并发送可见增量。</summary>
    public void RemoveMapObject(int mapId, Guid id)
    {
        var map = GetMap(mapId);
        _cache.PublishMapChange(map, map.Remove(id));
        DrainNotices();
    }

    /// <summary>提交完整实体候选并发送对应领域推送。</summary>
    public void PublishRoleState(RoleEntityTables update)
    {
        var previous = _cache.RequireRole(update.Role.Value.RoleId);
        var before = previous.Role.Value;
        var role = update.Role.Value;
        _cache.BagRules.Validate(update);
        if (role.MapId != before.MapId || !GetMap(role.MapId).Rules.Contains(role.X, role.Y))
            throw new InvalidOperationException("角色地图或位置无效，跨地图迁移尚未开放");
        var itemsChanged = !previous.Item.Items.OrderBy(row => row.Key).SequenceEqual(update.Item.Items.OrderBy(row => row.Key)) ||
            !previous.Equipment.Items.OrderBy(row => row.Key).SequenceEqual(update.Equipment.Items.OrderBy(row => row.Key));
        _cache.Commit(update);
        _cache.Session.Position = _cache.Session.Target = new Vector2(role.X, role.Y);
        Send(new RoleStateNotice { State = RoleMessages.World(update, _cache.Session.AcknowledgedAction) });
        if (itemsChanged) Send(RoleMessages.Items(update));
        if (role.Experience != before.Experience || role.NextExperience != before.NextExperience || role.BagExtraRows != before.BagExtraRows)
            Send(new RoleChangedNotice { Role = RoleMessages.Private(role) });
        PublishAoiPosition();
    }

    /// <summary>只提交角色资料并推送。</summary>
    public void PublishRoleProfile(RoleEntity update)
    {
        var tables = _cache.RequireRole(update.RoleId);
        var candidate = tables.WithRole(tables.Role.Value with { Name = update.Name, Level = update.Level, Gender = update.Gender, HeadId = update.HeadId });
        _cache.Commit(candidate, 1);
        Send(new RoleProfileNotice { Profile = RoleMessages.Profile(candidate.Role.Value) });
    }
    /// <summary>只提交可见生命魔力并推送。</summary>
    public void PublishRoleStatus(RoleEntity update)
    {
        var tables = _cache.RequireRole(update.RoleId);
        var candidate = tables.WithRole(tables.Role.Value with { Health = update.Health, MaxHealth = update.MaxHealth, Mana = update.Mana, MaxMana = update.MaxMana });
        _cache.Commit(candidate, 1);
        Send(new RoleStatusNotice { Status = RoleMessages.Status(candidate.Role.Value) });
    }
    /// <summary>只提交私有经验和扩容并推送。</summary>
    public void PublishRole(RoleEntity update)
    {
        var tables = _cache.RequireRole(update.RoleId);
        var candidate = tables.WithRole(tables.Role.Value with { Experience = update.Experience, NextExperience = update.NextExperience, BagExtraRows = update.BagExtraRows });
        _cache.BagRules.Validate(candidate);
        _cache.Commit(candidate);
        Send(new RoleChangedNotice { Role = RoleMessages.Private(candidate.Role.Value) });
    }
    /// <summary>主动提交当前位置并推送，保留资料与物品。</summary>
    public void PublishRolePosition(float x, float y)
    {
        var tables = _cache.Current ?? throw new InvalidOperationException("请先进入当前角色");
        if (!GetMap(tables.Role.Value.MapId).Rules.Contains(x, y)) throw new InvalidOperationException("角色位置无效");
        var candidate = tables.WithRole(tables.Role.Value with { X = x, Y = y });
        _cache.Commit(candidate, 1);
        _cache.Session.Position = _cache.Session.Target = new Vector2(x, y);
        Send(new RolePositionNotice { Position = RoleMessages.Position(candidate.Role.Value, _cache.Session.AcknowledgedAction) });
        PublishAoiPosition();
    }
    /// <summary>将当前位置引起的 AOI 差集交给传输。</summary>
    private void PublishAoiPosition()
    {
        if (_cache.Current is not { } tables || _cache.Session.Aoi is not { } aoi) return;
        var notice = aoi.Move(_cache.GetMap(tables.Role.Value.MapId), _cache.Session.Position.X, _cache.Session.Position.Y);
        if (notice is not null) Send(notice);
    }
    /// <summary>构造可供未来观察者渠道消费的公开装备载荷。</summary>
    internal static RoleEquipmentNotice CreateEquipmentNotice(RoleEntityTables tables) => new() { Equipment = RoleMessages.Equipment(tables) };
    /// <summary>公开装备投影只含配置编号。</summary>
    internal static RoleWorldEquipmentPb ToEquipmentMessage(RoleEntityTables tables) => RoleMessages.Equipment(tables);
    /// <summary>将处理器产生的内存消息按顺序发送。</summary>
    private void DrainNotices()
    {
        while (_cache.Notices.TryDequeue(out var message)) Send(message);
    }
    /// <summary>在唯一发送边界分配信封版本并编码字节。</summary>
    private void Send(IMessage response)
    {
        var revision = checked(++_revision);
        if (response is RoleItemsChangedNotice || response is SelectRoleRt { Success: true }) _cache.Session.ItemsRevision = revision;
        Outgoing?.Invoke(MessageCodec.Encode(response, _generation, false, revision));
    }
}
