using System.Threading.Tasks;
using System;
using Rpg.Config;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>登录业务的明确接入点；框架版不内置演示账号或认证规则。</summary>
internal sealed class LoginHandler : IActorHandler<LoginContext, ProtocolV2.Login, LoginRt>
{
    /// <summary>借用宿主同次启动的只读配置，后续业务从此读取配置。</summary>
    private readonly ConfigService _config;
    /// <summary>由 MockServerService 显式注入配置，不访问全局服务。</summary>
    internal LoginHandler(ConfigService config) => _config = config;
    /// <summary>尚未实现认证时返回明确失败，绝不把空实现当作登录成功。</summary>
    public ValueTask<LoginRt> HandleAsync(LoginContext context, ProtocolV2.Login request)
    {
        ObjectDisposedException.ThrowIf(_config.IsDisposed, _config);
        throw new ServerRequestException(ErrorCodePb.NotImplemented, "V2 登录业务尚未实现");
    }
}
