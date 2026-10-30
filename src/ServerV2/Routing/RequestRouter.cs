using System.Threading.Tasks;
using System;
using Google.Protobuf;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Connections;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Routing;

/// <summary>把已解码协议直接路由到模块，账号身份始终取自入口或服务端会话。</summary>
internal sealed class RequestRouter
{
    /// <summary>借用宿主持有的唯一分片目录。</summary>
    private readonly ServerModules _modules;
    /// <summary>绑定当前运行实例的模块。</summary>
    internal RequestRouter(ServerModules modules) => _modules = modules;

    /// <summary>登录从载荷定位账号，创建和删除只从服务端会话定位账号。</summary>
    internal async Task<DataResponse> RouteAsync(DataSessionSnapshot session, IMessage request, DataActorCall? call = null)
    {
        if (request is CreateRole or DeleteRole or EnterWorld)
        {
            if (session.Username is null) throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "连接尚未登录");
            var account = _modules.Login(session.Username);
            IMessage reply = request switch
            {
                CreateRole create => await account.Ask<CreateRoleRt>(create, call).ConfigureAwait(false),
                DeleteRole delete => await account.Ask<DeleteRoleRt>(delete, call).ConfigureAwait(false),
                EnterWorld enter => await account.Ask<EnterWorldRt>(enter, call).ConfigureAwait(false),
                _ => throw new InvalidOperationException()
            };
            return new(reply, null, request is EnterWorld selected ? selected.RoleId : null);
        }
        if (request is Move move)
        {
            if (session.Username is null || session.RoleId <= 0) throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "尚未进入角色");
            return new(await _modules.Role(session.RoleId).Ask<MoveRt>(move, call).ConfigureAwait(false), null);
        }
        if (request is not ProtocolV2.Login login)
            throw new ServerRequestException(ErrorCodePb.UnknownMessage, "请求尚未注册路由");
        string username;
        try { username = UsernameKey.Normalize(login.Username); }
        catch (ArgumentException error) { throw new ServerRequestException(ErrorCodePb.InvalidRequest, error.Message); }
        var response = await _modules.Login(username).Ask<LoginRt>(login, call).ConfigureAwait(false);
        return new(response, username);
    }

    /// <summary>后续账号操作不从请求提取 username，只按服务端快照定位。</summary>
    internal Task<TReply> AskAccountAsync<TReply>(DataSessionSnapshot session, IActorRequest<TReply> request)
    {
        if (session.Username is null) throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "连接尚未登录");
        return _modules.Login(session.Username).Ask(request);
    }

    /// <summary>当前请求独占的响应对象和成功后需要绑定的账号。</summary>
    internal sealed record DataResponse(IMessage Message, string? BindUsername, long? BindRoleId = null);
}
