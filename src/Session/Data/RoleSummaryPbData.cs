using System.IO;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>由 RoleSummaryPb 字段重新组装的不可变摘要，供选角界面读取。</summary>
public sealed record RoleSummaryPbData(long RoleId, string Name, int Level, RoleGender Gender)
{
    /// <summary>校验并复制协议摘要，不保留可变协议对象。</summary>
    public static RoleSummaryPbData FromPb(RoleSummaryPb message)
    {
        if (message is null) throw new InvalidDataException("角色摘要协议不能为空");
        var gender = message.Gender switch
        {
            RoleGenderPb.Male => RoleGender.Male,
            RoleGenderPb.Female => RoleGender.Female,
            _ => throw new InvalidDataException("角色摘要协议包含无效性别")
        };
        var data = new RoleSummaryPbData(message.RoleId, message.Name, message.Level, gender);
        data.Validate();
        return data;
    }

    /// <summary>校验摘要并创建独立协议对象，每次调用均返回新实例。</summary>
    public RoleSummaryPb ToPb()
    {
        Validate();
        return new RoleSummaryPb
        {
            RoleId = RoleId, Name = Name, Level = Level,
            Gender = Gender == RoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female
        };
    }

    /// <summary>确保双向转换只接受可供选角界面使用的有效摘要。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || string.IsNullOrWhiteSpace(Name) || Level <= 0 ||
            Gender is not (RoleGender.Male or RoleGender.Female))
            throw new InvalidDataException("角色摘要包含无效字段");
    }
}
