using System.IO;
using Godot;
using Rpg.Net.Message;

namespace Rpg.Session;

/// <summary>将线上的角色数据复制为客户端独立不可变快照。</summary>
public static class RoleMessageMapper
{
    /// <summary>校验基础字段并转换角色摘要，不保留可变协议引用。</summary>
    public static DataRole ToRole(DataRoleMessage message)
    {
        if (message is null || message.RoleId <= 0 || string.IsNullOrWhiteSpace(message.Name) || message.Level <= 0 ||
            message.MaxHealth <= 0 || message.Health < 0 || message.Health > message.MaxHealth ||
            message.MaxMana < 0 || message.Mana < 0 || message.Mana > message.MaxMana ||
            message.Experience < 0 || message.NextExperience <= 0 || !float.IsFinite(message.X) || !float.IsFinite(message.Y) ||
            message.Revision < 0 || message.AcknowledgedAction < 0)
            throw new InvalidDataException("角色协议包含无效字段");
        var gender = message.Gender switch
        {
            RoleGenderMessage.Male => RoleGender.Male,
            RoleGenderMessage.Female => RoleGender.Female,
            _ => throw new InvalidDataException("角色协议包含无效性别")
        };
        return new DataRole(message.RoleId, message.Name, message.Level, message.Health, message.MaxHealth,
            message.Mana, message.MaxMana, message.Experience, message.NextExperience, new Vector2(message.X, message.Y), gender);
    }

    /// <summary>复制完整权威状态，保留版本与移动确认序号。</summary>
    public static DataRoleState ToState(DataRoleMessage message) => new(ToRole(message),
        new Vector2(message.X, message.Y), message.Revision, message.AcknowledgedAction);
}
