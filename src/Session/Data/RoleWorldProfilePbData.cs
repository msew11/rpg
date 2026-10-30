using Rpg.Net;
using System.IO;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>独立于协议对象的角色可见资料快照。</summary>
public sealed record RoleWorldProfilePbData(long RoleId, string Name, int Level, RoleGender Gender,
    int HeadId, long Revision)
{
    /// <summary>校验并复制协议资料。</summary>
    public static RoleWorldProfilePbData FromPb(RoleWorldProfilePb message)
    {
        if (message is null) throw new InvalidDataException("角色资料协议不能为空");
        var gender = message.Gender switch
        {
            RoleGenderPb.Male => RoleGender.Male,
            RoleGenderPb.Female => RoleGender.Female,
            _ => throw new InvalidDataException("角色资料包含无效性别")
        };
        var data = new RoleWorldProfilePbData(message.RoleId, message.Name, message.Level,
            gender, message.HeadId, message.GetRevision());
        data.Validate();
        return data;
    }

    /// <summary>直接验证并复制 V2 结构，身份来自完整状态根节点。</summary>
    public static RoleWorldProfilePbData FromPb(ProtocolV2.RoleWorldProfilePb message, long roleId)
    {
        if (message is null) throw new InvalidDataException("角色资料协议不能为空");
        var gender = message.Gender switch
        {
            ProtocolV2.RoleGenderPb.Male => RoleGender.Male,
            ProtocolV2.RoleGenderPb.Female => RoleGender.Female,
            _ => throw new InvalidDataException("角色资料包含无效性别")
        };
        var data = new RoleWorldProfilePbData(roleId, message.Name, message.Level,
            gender, message.HeadId, 0);
        if (data.HeadId < 1) throw new InvalidDataException("V2 头型无效");
        data.Validate();
        return data;
    }

    /// <summary>创建新的可见资料协议对象。</summary>
    public RoleWorldProfilePb ToPb()
    {
        Validate();
        return new RoleWorldProfilePb { RoleId = RoleId, Name = Name, Level = Level,
            Gender = Gender == RoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female,
            HeadId = HeadId }.WithRevision(Revision);
    }

    /// <summary>拒绝不完整或非法资料。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || string.IsNullOrWhiteSpace(Name) || Level <= 0 || HeadId < 0 || HeadId > 74 ||
            Revision < 0 || Gender is not (RoleGender.Male or RoleGender.Female))
            throw new InvalidDataException("角色资料包含无效字段");
    }
}
