using System.Threading.Tasks;
using System;
using System.IO;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Modules.World.Storage;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.World;

/// <summary>按地图身份串行执行的模块，后续多个角色共用同一分片。</summary>
internal sealed class WorldActor : ModuleActor<WorldContext>
{
    /// <summary>借用所属模块集的定位能力，不拥有其生命周期。</summary>
    private readonly IActorLocator _actors;
    /// <summary>本分片固定地图身份。</summary>
    private readonly int _mapId;
    /// <summary>地图工作区借用的宿主配置。</summary>
    private readonly ConfigService _config;
    /// <summary>无业务状态的必需地图仓库。</summary>
    private readonly IWorldRepo _repo;
    /// <summary>绑定专用处理器表、地图身份与非空仓库。</summary>
    internal WorldActor(ConfigService config, IActorLocator actors, int mapId, IWorldRepo repo, ActorHandlers<WorldContext> handlers) : base(handlers)
    { ArgumentNullException.ThrowIfNull(repo); _mapId = mapId; _config = config; _actors = actors; _repo = repo; }
    /// <summary>先取已加载规格，再完整读取对象；未知地图不调用仓库。</summary>
    protected override async ValueTask<WorldContext> CreateContextAsync()
    {
        if (!_config.Map.Items.TryGetValue(_mapId, out var map)) throw new InvalidDataException($"V2 地图配置不存在：{_mapId}");
        var layout = new DataWorldLayout(_mapId, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
        var snapshot = await _repo.LoadAsync(layout).ConfigureAwait(false);
        return new(_config, _actors, layout, snapshot);
    }
    /// <summary>邮箱内直接保存，成功才接受基线；失败保留对象和待写块。</summary>
    protected override async ValueTask<object?> DispatchAsync(WorldContext context, object message)
    {
        if (message is DataCaptureWorldSave) return new DataWorldSave(context.Cache.Layout, context.Cache.Snapshot(), context.Cache.DirtyChunks);
        if (message is DataAcceptWorldSave accepted) { context.Cache.Objects.AcceptSaved(accepted.Save.Snapshot.Objects); return true; }
        if (message is DataWorldMembership membership) { context.Cache.Roles.Membership(membership.RoleId, membership.Online); return true; }
        if (message is not DataFlush) return await base.DispatchAsync(context, message).ConfigureAwait(false);
        var dirty = context.Cache.DirtyChunks;
        if (dirty.Count != 0)
        {
            await _repo.SaveAsync(context.Cache.Layout, context.Cache.Snapshot(), dirty).ConfigureAwait(false);
            context.Cache.AcceptSaved();
        }
        return true;
    }
}
