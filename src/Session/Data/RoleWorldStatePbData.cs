using System.IO;
using Godot;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>由 RoleWorldStatePb 字段重新组装的不可变数据，供角色缓存与世界读取。</summary>
/// <param name="RoleId">唯一角色 ID。</param>
/// <param name="Name">角色显示名称。</param>
/// <param name="Level">当前等级。</param>
/// <param name="Health">当前生命值。</param>
/// <param name="MaxHealth">生命上限。</param>
/// <param name="Mana">当前魔力。</param>
/// <param name="MaxMana">魔力上限。</param>
/// <param name="Experience">当前经验。</param>
/// <param name="NextExperience">升级所需经验。</param>
/// <param name="Position">服务器确认的逻辑位置。</param>
/// <param name="Gender">角色的基础身体性别。</param>
/// <param name="Revision">服务器权威状态版本。</param>
/// <param name="AcknowledgedAction">当前会话已确认的移动序号。</param>
/// <param name="HeadId">性别内头型编号，0 表示旧版默认头型。</param>
/// <param name="MapId">角色当前地图 ID。</param>
/// <param name="BagExtraRows">角色增加的背包行数。</param>
public sealed record RoleWorldStatePbData(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position, RoleGender Gender,
    long Revision, long AcknowledgedAction, int HeadId = 0, int BagExtraRows = 0, int MapId = 1)
{
    /// <summary>校验协议并复制角色属性、位置与版本，不保留协议对象。</summary>
    public static RoleWorldStatePbData FromPb(RoleWorldStatePb message)
    {
        if (message is null) throw new InvalidDataException("角色世界状态协议不能为空");
        var gender = message.Gender switch
        {
            RoleGenderPb.Male => RoleGender.Male,
            RoleGenderPb.Female => RoleGender.Female,
            _ => throw new InvalidDataException("角色世界状态协议包含无效性别")
        };
        var position = new Vector2(message.X, message.Y);
        var data = new RoleWorldStatePbData(message.RoleId, message.Name, message.Level, message.Health, message.MaxHealth,
            message.Mana, message.MaxMana, message.Experience, message.NextExperience, position, gender,
            message.Revision, message.AcknowledgedAction, message.HeadId, message.BagExtraRows, message.MapId == 0 ? 1 : message.MapId);
        data.Validate();
        return data;
    }

    /// <summary>校验状态并创建独立协议对象，坐标以当前权威 Position 为准。</summary>
    public RoleWorldStatePb ToPb()
    {
        Validate();
        return new RoleWorldStatePb
        {
            RoleId = RoleId, Name = Name, Level = Level,
            Health = Health, MaxHealth = MaxHealth, Mana = Mana, MaxMana = MaxMana,
            Experience = Experience, NextExperience = NextExperience,
            X = Position.X, Y = Position.Y,
            HeadId = HeadId,
            BagExtraRows = BagExtraRows, MapId = MapId,
            Gender = Gender == RoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female,
            Revision = Revision, AcknowledgedAction = AcknowledgedAction
        };
    }

    /// <summary>确保双向转换保留有效角色属性、逻辑坐标和权威版本。</summary>
    private void Validate()
    {
        if (MapId <= 0 || BagExtraRows < 0 || HeadId < 0 || HeadId > 74 || RoleId <= 0 || string.IsNullOrWhiteSpace(Name) || Level <= 0 ||
            MaxHealth <= 0 || Health < 0 || Health > MaxHealth ||
            MaxMana < 0 || Mana < 0 || Mana > MaxMana ||
            Experience < 0 || NextExperience <= 0 || !float.IsFinite(Position.X) || !float.IsFinite(Position.Y) ||
            Revision < 0 || AcknowledgedAction < 0 || Gender is not (RoleGender.Male or RoleGender.Female))
            throw new InvalidDataException("角色世界状态包含无效字段");
    }
}
