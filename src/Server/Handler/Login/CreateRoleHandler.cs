using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Rpg.Net.Message;
using Rpg.Server.Entity;
using Rpg.Server.Context;

namespace Rpg.Server.Handler.Login;

/// <summary>校验角色列表输入后提交初始实体表。</summary>
public sealed class CreateRoleHandler
{
    /// <summary>只处理已准备的实体缓存并返回响应，不执行 IO。</summary>
    public CreateRoleRt Handle(LoginContext cache, global::Rpg.Net.Message.CreateRole request)
    {
        var response = new CreateRoleRt();
        try
        {
            cache.RequireRoleList();
            var gender = request.Gender switch
            {
                RoleGenderPb.Male => ServerRoleGender.Male,
                RoleGenderPb.Female => ServerRoleGender.Female,
                _ => throw new ArgumentException("角色性别无效")
            };
            var rules = cache.Rules.MapLayouts[cache.Rules.InitialMapId];
            var candidate = RoleCreation.Create(cache, request.Name, gender, request.HeadId, cache.Rules.InitialMapId,
                Math.Min(gender == ServerRoleGender.Male ? 16016 : 16144, (long)rules.Width * rules.TileSize - 1),
                Math.Min(gender == ServerRoleGender.Male ? 16016 : 16080, (long)rules.Height * rules.TileSize - 1));
            var summaries = cache.EntityCache.Summaries.Values.Where(role => !role.IsDeleted).OrderBy(role => role.RoleId).Select(RoleMessages.Summary).ToList();
            summaries.Add(RoleMessages.Summary(RoleSummaryEntity.FromRole(candidate)));
            cache.Create(candidate);
            response.Roles.AddRange(summaries);
            response.Success = true;
        }
        catch (Exception error) { response.Error = error.Message; }
        return response;
    }
}
