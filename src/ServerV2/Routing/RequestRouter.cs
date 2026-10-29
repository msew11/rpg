using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Routing;

/// <summary>把已解码请求映射到模块消息，账号身份始终取自入口或服务端会话。</summary>
internal sealed class RequestRouter
{
    /// <summary>借用宿主持有的唯一分片目录。</summary>
    private readonly ServerModules _modules;
    /// <summary>绑定当前运行实例的模块。</summary>
    internal RequestRouter(ServerModules modules) => _modules = modules;

    /// <summary>首次登录从载荷定位账号，后续业务在对应协议加入时显式注册。</summary>
    internal async Task<DataResponse> RouteAsync(DataSessionSnapshot session, IMessage request)
    {
        if (request is not ProtocolV2.Login login)
            throw new ServerRequestException(ErrorCodePb.UnknownMessage, "请求尚未注册路由");
        string username;
        try { username = UsernameKey.Normalize(login.Username); }
        catch (ArgumentException error) { throw new ServerRequestException(ErrorCodePb.InvalidRequest, error.Message); }
        var result = await _modules.Login(username).Ask(new DataLogin(login.Password)).ConfigureAwait(false);
        var response = new LoginRt();
        if (!result.Roles.IsDefault)
            foreach (var role in result.Roles)
            {
                var summary = new RoleSummaryPb
                { RoleId = role.RoleId, Name = role.Name, Level = role.Level, Gender = (RoleGenderPb)role.Gender, HeadId = role.HeadId };
                if (!role.EquippedItems.IsDefault) summary.EquippedItems.AddRange(role.EquippedItems);
                response.Roles.Add(summary);
            }
        return new(response, username);
    }

    /// <summary>后续账号操作不从请求提取 username，只按服务端快照定位。</summary>
    internal Task<TReply> AskAccountAsync<TReply>(DataSessionSnapshot session, IActorRequest<TReply> request)
    {
        if (session.Username is null) throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "连接尚未登录");
        return _modules.Login(session.Username).Ask(request);
    }

    /// <summary>当前请求独占的响应对象和成功后需要绑定的账号。</summary>
    internal sealed record DataResponse(IMessage Message, string? BindUsername);
}
