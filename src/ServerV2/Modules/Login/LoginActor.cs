using System.Threading.Tasks;
using Rpg.Config;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>按规范账号分片的唯一摘要所有者，不保存某条连接的登录状态。</summary>
internal sealed class LoginActor : ModuleActor<LoginContext>
{
    /// <summary>不可变分片身份。</summary>
    private readonly string _username;
    /// <summary>只负责文件 IO 的构造依赖。</summary>
    private readonly IAccountRoleSummaryRepo _repo;
    /// <summary>创建工作区时借用宿主配置，不由本分片释放。</summary>
    private readonly ConfigService _config;
    /// <summary>绑定专用处理器与仓库，不预加载。</summary>
    internal LoginActor(ConfigService config, string username, IAccountRoleSummaryRepo repo, ActorHandlers<LoginContext> handlers) : base(handlers)
    { _username = username; _repo = repo; _config = config; }
    /// <summary>首次消息进入 INIT，完整加载后才发布工作区。</summary>
    protected override async ValueTask<LoginContext> CreateContextAsync() => new(_config, _username, await _repo.LoadAsync(_username).ConfigureAwait(false));
    /// <summary>保存与业务同邮箱串行；仅成功后清除待写标记。</summary>
    protected override async ValueTask<object?> DispatchAsync(LoginContext context, object message)
    {
        if (message is not DataFlush) return await base.DispatchAsync(context, message).ConfigureAwait(false);
        if (context.SummariesDirty)
        {
            await _repo.SaveAsync(context.Summaries).ConfigureAwait(false);
            context.AcceptSaved();
        }
        return true;
    }
}
