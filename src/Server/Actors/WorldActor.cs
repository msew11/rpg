using System;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Server.EntityCache;
using Rpg.Server.Context;
using Rpg.Net.Message;
using Rpg.Server.Handler.World;
using Rpg.Server.Map;

namespace Rpg.Server.Actors;

/// <summary>按地图身份分片的唯一可写世界，串行处理全部对象与跨角色事务。</summary>
internal sealed class WorldActor : BaseActor
{
    /// <summary>本分片地图身份。</summary>
    internal int MapId { get; }
    /// <summary>本地图加载规则。</summary>
    private readonly MapLayout _layout;
    /// <summary>只读配置。</summary>
    private readonly DataServerRules _rules;
    /// <summary>文件仓库。</summary>
    private readonly RepoServerMap _repo;
    /// <summary>初始化完成后独占的世界缓存。</summary>
    private ServerWorldCache _cache = null!;

    /// <summary>本 Actor 复用的SelectRole请求处理器。</summary>
    private readonly SelectRoleHandler _selectRoleHandler = new();
    /// <summary>本 Actor 复用的DropItem请求处理器。</summary>
    private readonly DropItemHandler _dropItemHandler = new();
    /// <summary>本 Actor 复用的PickupItem请求处理器。</summary>
    private readonly PickupItemHandler _pickupItemHandler = new();
    /// <summary>本 Actor 复用的ResyncMapAoi请求处理器。</summary>
    private readonly ResyncMapAoiHandler _resyncMapAoiHandler = new();

    /// <summary>配置固定身份，不在构造中读取地图。</summary>
    internal WorldActor(MapLayout layout, DataServerRules rules, RepoServerMap repo)
    { MapId = layout.MapId; _layout = layout; _rules = rules; _repo = repo; }

    /// <summary>只加载本地图，不初始化其他地图。</summary>
    protected override ValueTask InitializeAsync()
    {
        _cache = new(new ServerMap(_layout, _repo, _rules.BagRules.Items.ToDictionary(item => item.Key, item => item.Value.MaxStack)));
        return ValueTask.CompletedTask;
    }

    /// <summary>只接受身份匹配的消息，工作副本在回合结束后交还 RoleActor。</summary>
    protected override ValueTask<object?> ReceiveAsync(object message)
    {
        if (message is DataMapWork map && map.MapId == MapId) return ValueTask.FromResult(map.Work(_cache));
        var (role, work) = message switch
        {
            DataWorldWork value when value.MapId == MapId => (value.Role, value.Work),
            DataWorldRequest value when value.MapId == MapId => (value.Role, (Func<WorldContext, object?>)(context => Handle(context, value.Request))),
            _ => throw new InvalidOperationException("地图分片身份不符")
        };
        if (role.Role.Value.MapId != MapId) throw new InvalidOperationException("角色所属地图不符");
        var context = new WorldContext(_cache, new RoleContext(role, _rules));
        var response = work(context);
        return ValueTask.FromResult<object?>(new DataRoleResult(response, context.Role.EntityCache, context.Role.Notices.ToArray()));
    }

    /// <summary>地图请求只在所属 WorldActor 邮箱中分发。</summary>
    private object Handle(WorldContext context, Google.Protobuf.IMessage request) => request switch
    {
        SelectRole value => _selectRoleHandler.Handle(context, value),
        DropItem value => _dropItemHandler.Handle(context, value),
        PickupItem value => _pickupItemHandler.Handle(context, value),
        ResyncMapAoi value => _resyncMapAoiHandler.Handle(context, value),
        _ => throw new InvalidOperationException("地图请求类型无效")
    };
}
