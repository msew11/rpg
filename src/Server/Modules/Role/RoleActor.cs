using System.Threading.Tasks;
using Rpg.Config;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Role;

/// <summary>按角色身份独占三表，初始化及保存均在所属邮箱边界执行。</summary>
internal sealed class RoleActor : ModuleActor<RoleContext>
{
    /// <summary>借用所属模块集的定位能力，不拥有其生命周期。</summary>
    private readonly IActorLocator _actors;
    /// <summary>本分片固定角色身份。</summary>
    private readonly long _roleId;
    /// <summary>无业务状态的三表存储。</summary>
    private readonly IRoleStorage _storage;
    /// <summary>构建角色工作区时借用宿主配置。</summary>
    private readonly ConfigService _config;
    /// <summary>绑定专用处理器表、角色身份和存储。</summary>
    internal RoleActor(ConfigService config, IActorLocator actors, long roleId, IRoleStorage storage, ActorHandlers<RoleContext> handlers) : base(handlers)
    { _roleId = roleId; _storage = storage; _config = config; _actors = actors; }
    /// <summary>已有三表完整加载或明确全缺失后进入 RUN，不发布部分加载状态。</summary>
    protected override async ValueTask<RoleContext> CreateContextAsync() => new(_config, _actors, _roleId, await _storage.TryLoadExistingAsync(_roleId).ConfigureAwait(false));
    /// <summary>串行保存本角色变化表，失败不改变内存和保存基线。</summary>
    protected override async ValueTask<object?> DispatchAsync(RoleContext context, object message)
    {
        if (message is DataCaptureRoleSave) return new DataRoleSave(_roleId, context.Cache.IsInitialized ? context.Cache.Snapshot() : null, context.Cache.DirtyTables);
        if (message is DataAcceptRoleSave accepted)
        {
            var saved = accepted.Save;
            if (saved.Snapshot is { } snapshot)
            {
                if (saved.Tables.HasFlag(DataRoleTables.Role)) context.Cache.Role.AcceptSaved(snapshot.Role);
                if (saved.Tables.HasFlag(DataRoleTables.Item)) context.Cache.Items.AcceptSaved(snapshot.Items);
                if (saved.Tables.HasFlag(DataRoleTables.Equipment)) context.Cache.Equipments.AcceptSaved(snapshot.Equipments);
            }
            return true;
        }
        if (message is DataRoleControl control)
        { context.Cache.Pickup = null; context.Cache.Movement.Stop(context.Cache.Role, control.ServerTimeMs, control.Speed); return true; }
        if (message is ActorProtocol.PrepareRole prepare)
        {
            context.Cache.Prepare(prepare.Summary, context.Config);
            await SaveAsync(context).ConfigureAwait(false);
            return await base.DispatchAsync(context, message).ConfigureAwait(false);
        }
        if (message is not DataFlush) return await base.DispatchAsync(context, message).ConfigureAwait(false);
        await SaveAsync(context).ConfigureAwait(false);
        return true;
    }
    /// <summary>只保存已建立的候选，失败保留全部待写数据以便重试。</summary>
    private async ValueTask SaveAsync(RoleContext context)
    {
        var dirty = context.Cache.DirtyTables;
        if (dirty != DataRoleTables.None)
        {
            await _storage.SaveAsync(_roleId, context.Cache.Snapshot(), dirty).ConfigureAwait(false);
            context.Cache.AcceptSaved();
        }
    }
}
