using Rpg.Server.Modules.Login.Utils;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Login.Handler;

/// <summary>验证演示凭据并返回当前账号的完整可见角色摘要。</summary>
internal sealed class LoginHandler : IActorHandler<LoginContext, global::Rpg.Protocol.Login, LoginRt>
{
    /// <summary>账号使用规范分片键，密码原样匹配；失败不建立连接绑定。</summary>
    public ValueTask<LoginRt> HandleAsync(LoginContext context, global::Rpg.Protocol.Login request)
    {
        if (!DefaultAccounts.Accepts(context.Cache.Username, request.Password))
            throw new ServerRequestException(ErrorCodePb.InvalidCredentials, "用户名或密码错误");
        var response = new LoginRt();
        response.Roles.AddRange(RoleSummaries.Visible(context));
        return ValueTask.FromResult(response);
    }
}
