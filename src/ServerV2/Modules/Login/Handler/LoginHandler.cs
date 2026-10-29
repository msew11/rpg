using System.Threading.Tasks;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login.Handler;

/// <summary>登录业务的明确接入点；框架版不内置演示账号或认证规则。</summary>
internal sealed class LoginHandler : IActorHandler<LoginContext, ProtocolV2.Login, LoginRt>
{
    /// <summary>尚未实现认证时返回明确失败，绝不把空实现当作登录成功。</summary>
    public ValueTask<LoginRt> HandleAsync(LoginContext context, ProtocolV2.Login request)
    {
        throw new ServerRequestException(ErrorCodePb.NotImplemented, "V2 登录业务尚未实现");
    }
}
