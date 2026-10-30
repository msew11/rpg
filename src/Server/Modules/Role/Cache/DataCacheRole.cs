using Rpg.Server.Modules.Common.Cache;
using Rpg.Server.Modules.Role.Data;

namespace Rpg.Server.Modules.Role.Cache;

/// <summary>角色基础表的独占数据值和保存基线。</summary>
public sealed class DataCacheRole : DataCache<DataRole>
{
    /// <summary>按可信摘要及配置建立首次角色，出生点裁剪到有效逻辑像素。</summary>
    internal static DataRole CreateInitial(Rpg.Protocol.RoleSummaryPb summary, Rpg.Config.Data.MapCfg map)
    {
        if (string.IsNullOrWhiteSpace(summary.Name) || summary.Level <= 0 || summary.HeadId is < 1 or > 74 ||
            summary.Gender is not (Rpg.Protocol.RoleGenderPb.Male or Rpg.Protocol.RoleGenderPb.Female))
            throw new System.IO.InvalidDataException("首次角色摘要外观无效");
        var female = summary.Gender == Rpg.Protocol.RoleGenderPb.Female;
        return new DataRole { RoleId = summary.RoleId, Name = summary.Name, Level = summary.Level,
            Gender = (Common.Data.RoleGender)summary.Gender, HeadId = summary.HeadId,
            Health = 100, MaxHealth = 100, Mana = 60, MaxMana = 60, Experience = 0, NextExperience = 500,
            BagExtraRows = 0, NextInstanceId = 1, MapId = map.Id,
            X = System.Math.Clamp(female ? 16144f : 16016f, 0, (long)map.Width * map.TileSize - 1),
            Y = System.Math.Clamp(female ? 16080f : 16016f, 0, (long)map.Height * map.TileSize - 1) };
    }
    /// <summary>用完整加载的表内容建立保存基线。</summary>
    internal DataCacheRole(DataRole value) : base(value) { }
}
