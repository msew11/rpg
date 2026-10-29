using System;
using System.Linq;
using Rpg.Net.Message;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.Login;

/// <summary>提交已准备角色的内存墓碑。</summary>
public sealed class DeleteRoleHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public DeleteRoleRt Handle(LoginContext cache, DeleteRole request)
    {
        var response = new DeleteRoleRt();
        try
        {
            cache.RequireRoleList();
            var summaries = cache.EntityCache.Summaries.Values.Where(role => !role.IsDeleted && role.RoleId != request.RoleId)
                .OrderBy(role => role.RoleId).Select(RoleMessages.Summary).ToArray();
            cache.Delete(request.RoleId);
            response.Roles.AddRange(summaries);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
