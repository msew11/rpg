using System;
using System.Threading.Tasks;
using Rpg.Net.Message;
using Rpg.Server.EntityCache;
using Rpg.Server.Entity;
using Rpg.Server.Context;
using Rpg.Server.Handler.Role;

namespace Rpg.Server.Actors;

/// <summary>按角色身份独占实体和模拟状态，通过 Ask 将地图事务交给所属世界。</summary>
internal sealed class RoleActor : BaseActor
{
    /// <summary>固定角色分片身份。</summary>
    internal long RoleId { get; }
    /// <summary>角色文件仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>领取新建实体及验证摘要的登录 Actor。</summary>
    private readonly LoginActor _login;
    /// <summary>按地图身份获取唯一 WorldActor。</summary>
    private readonly Func<int, WorldActor> _world;
    /// <summary>只读业务规则。</summary>
    private readonly DataServerRules _rules;
    /// <summary>本回合结束后唯一保留的角色缓存。</summary>
    private ServerRoleCache _cache = new();

    /// <summary>本 Actor 复用的Move请求处理器。</summary>
    private readonly MoveHandler _moveHandler = new();
    /// <summary>本 Actor 复用的Gm请求处理器。</summary>
    private readonly GmHandler _gmHandler = new();
    /// <summary>本 Actor 复用的MoveItem请求处理器。</summary>
    private readonly MoveItemHandler _moveItemHandler = new();
    /// <summary>本 Actor 复用的EquipItem请求处理器。</summary>
    private readonly EquipItemHandler _equipItemHandler = new();

    /// <summary>构造固定分片，加载只发生在 INIT。</summary>
    internal RoleActor(long roleId, RepoServerRole repo, LoginActor login, Func<int, WorldActor> world,
        DataServerRules rules)
    { RoleId = roleId; _repo = repo; _login = login; _world = world; _rules = rules; }

    /// <summary>领取或加载本角色三表后，按角色 MapId Ask 初始化地图。</summary>
    protected override async ValueTask InitializeAsync()
    {
        // 地图初始化成功后才接管新建实体，失败仍由登录缓存保留待写数据。
        var seed = await _login.Ask<DataRoleSeed>(new DataClaimRole(RoleId)).ConfigureAwait(false);
        _cache.Tables = seed.Tables ?? _repo.Load(RoleId);
        _cache.Dirty = seed.Tables is null ? 0 : 3;
        _rules.BagRules.Validate(_cache.Tables);
        await _world(_cache.Tables.Role.Value.MapId).Ask<bool>(new DataMapWork(_cache.Tables.Role.Value.MapId, _ => true)).ConfigureAwait(false);
        await _login.Ask<bool>(new DataLoginWork(context => { context.EntityCache.Created.Remove(RoleId); return true; })).ConfigureAwait(false);
    }

    /// <summary>在自己回合路由角色 Handler，需要地图时只移交独立工作副本。</summary>
    protected override async ValueTask<object?> ReceiveAsync(object message)
    {
        if (message is DataRoleWork work && work.RoleId == RoleId) return InRole(work.Work);
        if (message is DataRoleWorldWork worldWork && worldWork.RoleId == RoleId)
            return await InWorld(id => new DataWorldWork(id, _cache.Copy(), worldWork.Work)).ConfigureAwait(false);
        if (message is not DataRoleRequest request || request.RoleId != RoleId) throw new InvalidOperationException("角色分片身份不符");
        if (request.Request is SelectRole) _cache.Session.Authenticated = true;
        if (request.Request is SelectRole or DropItem or PickupItem or ResyncMapAoi)
            return await InWorld(id => new DataWorldRequest(id, _cache.Copy(), request.Request)).ConfigureAwait(false);
        return InRole(cache => request.Request switch
        {
            Move move => _moveHandler.Handle(cache, move),
            Gm gm => _gmHandler.Handle(cache, gm),
            MoveItem item => _moveItemHandler.Handle(cache, item),
            EquipItem equip => _equipItemHandler.Handle(cache, equip),
            _ => throw new InvalidOperationException("角色 Actor 不支持此请求")
        });
    }

    /// <summary>纯角色请求在自己的异步回合处理，结果采用副本发布。</summary>
    private DataRoleResult InRole(Func<RoleContext, object?> work)
    {
        var previous = _cache.Tables!;
        var context = new RoleContext(_cache.Copy(), _rules);
        var response = work(context);
        _cache = context.EntityCache;
        PublishSummary(previous);
        return new(response, _cache, context.Notices.ToArray());
    }

    /// <summary>暂停本角色回合，地图串行处理副本后接回全部结果。</summary>
    private async Task<DataRoleResult> InWorld(Func<int, object> message)
    {
        var previous = _cache.Tables!;
        var id = _cache.Tables!.Role.Value.MapId;
        var result = await _world(id).Ask<DataRoleResult>(message(id)).ConfigureAwait(false);
        _cache = result.Role;
        PublishSummary(previous);
        return result;
    }

    /// <summary>只有展示字段或物品表变化才发布摘要，空回合和位置推进不增加登录消息。</summary>
    private void PublishSummary(RoleEntityTables previous)
    {
        var current = _cache.Tables!;
        var before = previous.Role.Value;
        var after = current.Role.Value;
        if (!ReferenceEquals(previous.Item, current.Item) || before.Name != after.Name || before.Level != after.Level ||
            before.Gender != after.Gender || before.HeadId != after.HeadId || before.IsDeleted != after.IsDeleted)
            _login.Tell(new DataSummaryChanged(RoleSummaryEntity.FromRole(current)));
    }
}
