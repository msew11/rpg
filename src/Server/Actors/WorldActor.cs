using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;
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
    private readonly BagRules _rules;
    /// <summary>文件仓库。</summary>
    private readonly RepoServerMap _repo;
    /// <summary>全量不可变地图规格。</summary>
    private readonly MapLayout[] _maps;
    /// <summary>新角色初始地图。</summary>
    private readonly int _initialMapId;
    /// <summary>初始化完成后独占的世界缓存。</summary>
    private ServerWorldCache _cache = null!;

    /// <summary>配置固定身份，不在构造中读取地图。</summary>
    internal WorldActor(MapLayout layout, BagRules rules, RepoServerMap repo, MapLayout[] maps, int initialMapId)
    { MapId = layout.MapId; _layout = layout; _rules = rules; _repo = repo; _maps = maps; _initialMapId = initialMapId; }

    /// <summary>只加载本地图，不初始化其他地图。</summary>
    protected override ValueTask InitializeAsync()
    {
        _cache = new(new ServerMap(_layout, _repo, _rules.Items.ToDictionary(item => item.Key, item => item.Value.MaxStack)));
        return ValueTask.CompletedTask;
    }

    /// <summary>只接受身份匹配的消息，工作副本在回合结束后交还 RoleActor。</summary>
    protected override ValueTask<object?> ReceiveAsync(object message)
    {
        if (message is DataMapWork map && map.MapId == MapId) return ValueTask.FromResult(map.Work(_cache));
        if (message is not DataWorldWork work || work.MapId != MapId) throw new InvalidOperationException("地图分片身份不符");
        var context = new ServerRequestContext(_rules, _maps, _initialMapId, role: work.Role, world: _cache);
        var response = work.Work(context);
        return ValueTask.FromResult<object?>(new DataRoleResult(response, context.Role, context.Notices.ToArray()));
    }
}
