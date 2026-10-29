using System.Threading.Tasks;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Role;

/// <summary>按角色身份独占三表，初始化及保存均在所属邮箱边界执行。</summary>
internal sealed class RoleActor : ModuleActor<RoleContext>
{
    /// <summary>本分片固定角色身份。</summary>
    private readonly long _roleId;
    /// <summary>无业务状态的三表仓库。</summary>
    private readonly IRoleRepo _repo;
    /// <summary>构建角色工作区时借用宿主配置。</summary>
    private readonly ConfigService? _config;
    /// <summary>绑定专用处理器表、角色身份和仓库。</summary>
    internal RoleActor(long roleId, IRoleRepo repo, ActorHandlers<RoleContext> handlers, ConfigService? config = null) : base(handlers)
    { _roleId = roleId; _repo = repo; _config = config; }
    /// <summary>三表全部加载成功后才进入 RUN。</summary>
    protected override async ValueTask<RoleContext> CreateContextAsync() => new(_roleId, await _repo.LoadAsync(_roleId).ConfigureAwait(false), _config);
    /// <summary>串行保存本角色变化表，失败不改变内存和保存基线。</summary>
    protected override async ValueTask<object?> DispatchAsync(RoleContext context, object message)
    {
        if (message is not DataFlush) return await base.DispatchAsync(context, message).ConfigureAwait(false);
        if (context.DirtyTables != DataRoleTables.None)
        {
            await _repo.SaveAsync(_roleId, context.Snapshot(), context.DirtyTables).ConfigureAwait(false);
            context.AcceptSaved();
        }
        return true;
    }
}
