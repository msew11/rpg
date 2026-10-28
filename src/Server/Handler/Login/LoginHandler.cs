using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Handler.Login;

/// <summary>验证演示账号并读取预加载摘要。</summary>
public sealed class LoginHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public LoginRt Handle(ServerRequestContext cache, global::Rpg.Net.Message.Login request)
    {
        cache.Session.Authenticated = false;
        cache.Session.Reset();
        var response = new LoginRt();
        try
        {
            if (request.Username != "demo" || request.Password != "demo") throw new InvalidOperationException("用户名或密码错误");
            if (!cache.Login.Loaded) throw new InvalidOperationException("角色摘要尚未加载");
            response.Roles.AddRange(cache.Login.Summaries.Values.Where(role => !role.IsDeleted).OrderBy(role => role.RoleId).Select(RoleMessages.Summary));
            response.Success = cache.Session.Authenticated = true;
        }
        catch (Exception error) { response.Error = error.Message; response.Roles.Clear(); }
        return response;
    }
}
