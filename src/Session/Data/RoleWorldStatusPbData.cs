using Rpg.Net;
using System.IO;
using Rpg.Protocol;

namespace Rpg.Session.Data;

/// <summary>独立于协议对象的可见生命和魔力快照。</summary>
public sealed record RoleWorldStatusPbData(long RoleId, int Health, int MaxHealth, int Mana, int MaxMana,
    long Revision)
{
    /// <summary>直接验证并复制 结构，身份来自完整状态根节点。</summary>
    public static RoleWorldStatusPbData FromPb(global::Rpg.Protocol.RoleWorldStatusPb message, long roleId)
    {
        if (message is null) throw new InvalidDataException("角色状态协议不能为空");
        var data = new RoleWorldStatusPbData(roleId, message.Health, message.MaxHealth,
            message.Mana, message.MaxMana, 0);
        data.Validate();
        return data;
    }

    /// <summary>创建新的可见状态协议对象。</summary>
    public RoleWorldStatusPb ToPb()
    {
        Validate();
        return new RoleWorldStatusPb { Health = Health, MaxHealth = MaxHealth,
            Mana = Mana, MaxMana = MaxMana };
    }

    /// <summary>拒绝无效生命、魔力和版本。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || MaxHealth <= 0 || Health < 0 || Health > MaxHealth || MaxMana < 0 ||
            Mana < 0 || Mana > MaxMana || Revision < 0)
            throw new InvalidDataException("角色状态包含无效字段");
    }
}
