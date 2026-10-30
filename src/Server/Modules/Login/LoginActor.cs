using System.Threading.Tasks;
using Rpg.Config;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login;

/// <summary>按规范账号分片的唯一摘要所有者，不保存某条连接的登录状态。</summary>
internal sealed class LoginActor : ModuleActor<LoginContext>
{
    /// <summary>借用所属模块集的定位能力，不拥有其生命周期。</summary>
    private readonly IActorLocator _actors;
    /// <summary>不可变分片身份。</summary>
    private readonly string _username;
    /// <summary>只负责文件 IO 的构造依赖。</summary>
    private readonly ILoginStorage _storage;
    /// <summary>创建工作区时借用宿主配置，不由本分片释放。</summary>
    private readonly ConfigService _config;
    /// <summary>绑定专用处理器与存储，不预加载。</summary>
    internal LoginActor(ConfigService config, IActorLocator actors, string username, ILoginStorage storage, ActorHandlers<LoginContext> handlers) : base(handlers)
    { _username = username; _storage = storage; _config = config; _actors = actors; }
    /// <summary>首次消息进入 INIT，完整加载后才发布工作区。</summary>
    protected override async ValueTask<LoginContext> CreateContextAsync() => new(_config, _actors, _username, new(await _storage.LoadAsync(_username).ConfigureAwait(false)));
    /// <summary>取号和保存由基础设施在同邮箱串行准备，Handler 只提交内存。</summary>
    protected override async ValueTask<object?> DispatchAsync(LoginContext context, object message)
    {
        if (message is DataCaptureLoginSave) return new DataLoginSave(_username, context.Cache.Snapshot().Summaries, context.Cache.SummariesDirty);
        if (message is DataAcceptLoginSave accepted) { context.Cache.Summaries.AcceptSaved(accepted.Save.Summaries); return true; }
        if (message is global::Rpg.Protocol.CreateRole create)
        {
            context.Cache.Summaries.ValidateCreation(create.Name, (RoleGender)create.Gender, create.HeadId);
            context.AllocatedRoleId = await _storage.NextRoleIdAsync().ConfigureAwait(false);
            try { return await base.DispatchAsync(context, message).ConfigureAwait(false); }
            finally { context.AllocatedRoleId = 0; }
        }
        if (message is global::Rpg.Protocol.EnterWorld enter)
        {
            context.Cache.Summaries.Owned(enter.RoleId);
            await SaveAsync(context).ConfigureAwait(false);
            return await base.DispatchAsync(context, message).ConfigureAwait(false);
        }
        if (message is not DataFlush) return await base.DispatchAsync(context, message).ConfigureAwait(false);
        await SaveAsync(context).ConfigureAwait(false);
        return true;
    }
    /// <summary>在可信首次建档前或显式写回时保存摘要基线。</summary>
    private async ValueTask SaveAsync(LoginContext context)
    {
        if (context.Cache.SummariesDirty)
        {
            await _storage.SaveAsync(_username, context.Cache.Snapshot().Summaries).ConfigureAwait(false);
            context.Cache.AcceptSaved();
        }
    }
}
