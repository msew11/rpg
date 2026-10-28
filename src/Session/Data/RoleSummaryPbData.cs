using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Rpg.Const;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>由 RoleSummaryPb 字段重新组装的不可变摘要，供选角界面读取。</summary>
/// <param name="HeadId">性别内头型编号，0 表示旧版默认头型。</param>
public sealed record RoleSummaryPbData(long RoleId, string Name, int Level, RoleGender Gender, int HeadId = 0)
{
    /// <summary>由摘要独占的装备编号副本。</summary>
    private readonly IReadOnlyList<int> _equippedItems = Array.Empty<int>();
    /// <summary>只供角色预览使用的已穿道具配置编号。</summary>
    public IReadOnlyList<int> EquippedItems
    {
        get => _equippedItems;
        init => _equippedItems = Array.AsReadOnly((value ?? throw new InvalidDataException("摘要装备列表不能为空")).ToArray());
    }

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
        var data = new RoleSummaryPbData(message.RoleId, message.Name, message.Level, gender, message.HeadId)
        { EquippedItems = message.EquippedItems };
        data.Validate();
        return data;
    }

    /// <summary>校验摘要并创建独立协议对象，每次调用均返回新实例。</summary>
    public RoleSummaryPb ToPb()
    {
        Validate();
        var message = new RoleSummaryPb
        {
            RoleId = RoleId, Name = Name, Level = Level,
            HeadId = HeadId,
            Gender = Gender == RoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female
        };
        message.EquippedItems.AddRange(EquippedItems);
        return message;
    }

    /// <summary>确保双向转换只接受可供选角界面使用的有效摘要。</summary>
    private void Validate()
    {
        if (EquippedItems.Any(id => id <= 0) || HeadId < 0 || HeadId > 74 || RoleId <= 0 || string.IsNullOrWhiteSpace(Name) || Level <= 0 ||
            Gender is not (RoleGender.Male or RoleGender.Female))
            throw new InvalidDataException("角色摘要包含无效字段");
    }
}
