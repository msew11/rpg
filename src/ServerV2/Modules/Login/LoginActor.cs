using System.Threading.Tasks;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>按规范账号分片的唯一摘要所有者，不保存某条连接的登录状态。</summary>
internal sealed class LoginActor : ModuleActor<LoginContext>
{
    /// <summary>不可变分片身份。</summary>
    private readonly string _username;
    /// <summary>绑定本分片的专用处理器。</summary>
    internal LoginActor(string username, ActorHandlers<LoginContext> handlers) : base(handlers) => _username = username;
    /// <summary>首次消息创建账号工作区；真实加载后续从此边界接入。</summary>
    protected override ValueTask<LoginContext> CreateContextAsync() => ValueTask.FromResult(new LoginContext(_username));
}
