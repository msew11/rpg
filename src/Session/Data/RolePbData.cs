using Rpg.Net;
using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>当前玩家私有的角色玩法数据快照。</summary>
public sealed record RolePbData(long RoleId, int Experience, int NextExperience, int BagExtraRows,
    long Revision)
{
    /// <summary>校验并复制私有角色数据。</summary>
    public static RolePbData FromPb(RolePb message)
    {
        if (message is null) throw new InvalidDataException("私有角色协议不能为空");
        var data = new RolePbData(message.RoleId, message.Experience, message.NextExperience,
            message.BagExtraRows, message.GetRevision());
        data.Validate();
        return data;
    }

    /// <summary>直接验证并复制 V2 结构，身份来自完整状态根节点。</summary>
    public static RolePbData FromPb(ProtocolV2.RolePb message, long roleId)
    {
        if (message is null) throw new InvalidDataException("私有角色协议不能为空");
        var data = new RolePbData(roleId, message.Experience, message.NextExperience,
            message.BagExtraRows, 0);
        data.Validate();
        return data;
    }

    /// <summary>创建新的私有角色协议对象。</summary>
    public RolePb ToPb()
    {
        Validate();
        return new RolePb { RoleId = RoleId, Experience = Experience,
            NextExperience = NextExperience, BagExtraRows = BagExtraRows }.WithRevision(Revision);
    }

    /// <summary>拒绝无效经验、容量和版本。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || Experience < 0 || NextExperience <= 0 || BagExtraRows < 0 || Revision < 0)
            throw new InvalidDataException("私有角色数据包含无效字段");
    }
}
