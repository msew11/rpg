using System.IO;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>独立于协议对象的角色资料快照。</summary>
public sealed record RoleWorldProfilePbData(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, RoleGender Gender, int HeadId,
    int BagExtraRows, long Revision)
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
        var data = new RoleWorldProfilePbData(message.RoleId, message.Name, message.Level, message.Health,
            message.MaxHealth, message.Mana, message.MaxMana, message.Experience, message.NextExperience,
            gender, message.HeadId, message.BagExtraRows, message.Revision);
        data.Validate();
        return data;
    }

    /// <summary>创建新的角色资料协议对象。</summary>
    public RoleWorldProfilePb ToPb()
    {
        Validate();
        return new RoleWorldProfilePb { RoleId = RoleId, Name = Name, Level = Level, Health = Health,
            MaxHealth = MaxHealth, Mana = Mana, MaxMana = MaxMana, Experience = Experience,
            NextExperience = NextExperience, Gender = Gender == RoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female,
            HeadId = HeadId, BagExtraRows = BagExtraRows, Revision = Revision };
    }

    /// <summary>拒绝不完整或非法资料。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || string.IsNullOrWhiteSpace(Name) || Level <= 0 || MaxHealth <= 0 ||
            Health < 0 || Health > MaxHealth || MaxMana < 0 || Mana < 0 || Mana > MaxMana ||
            Experience < 0 || NextExperience <= 0 || HeadId < 0 || HeadId > 74 || BagExtraRows < 0 ||
            Revision < 0 || Gender is not (RoleGender.Male or RoleGender.Female))
            throw new InvalidDataException("角色资料包含无效字段");
    }
}
