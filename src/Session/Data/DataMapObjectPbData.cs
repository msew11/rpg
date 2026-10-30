using System;
using System.IO;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>地图对象的不可变客户端载荷，不保留 protobuf 引用。</summary>
/// <param name="ObjectId">稳定对象身份。</param>
/// <param name="Revision">对象权威版本。</param>
/// <param name="X">逻辑横坐标。</param>
/// <param name="Y">逻辑纵坐标。</param>
/// <param name="MonsterTypeId">怪物类型，道具时为空。</param>
/// <param name="ItemId">地面道具编号，怪物时为零。</param>
/// <param name="Count">地面数量，怪物时为零。</param>
public sealed record DataMapObjectPbData(Guid ObjectId, long Revision, float X, float Y, string? MonsterTypeId, int ItemId, int Count)
{
    /// <summary>验证互斥载荷并复制；坐标所属块由 AOI 状态检查。</summary>
    public static DataMapObjectPbData FromPb(MapObjectPb value)
    {
        if (!Guid.TryParseExact(value.ObjectId, "N", out var id) || id == Guid.Empty || value.Revision <= 0 ||
            !float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new InvalidDataException("地图对象身份、版本或位置无效");
        return value.PayloadCase switch
        {
            MapObjectPb.PayloadOneofCase.Monster when !string.IsNullOrWhiteSpace(value.Monster.MonsterTypeId) =>
                new(id, value.Revision, value.X, value.Y, value.Monster.MonsterTypeId, 0, 0),
            MapObjectPb.PayloadOneofCase.GroundItem when value.GroundItem.ItemId > 0 && value.GroundItem.Count > 0 =>
                new(id, value.Revision, value.X, value.Y, null, value.GroundItem.ItemId, value.GroundItem.Count),
            _ => throw new InvalidDataException("地图对象载荷无效")
        };
    }
    /// <summary>直接校验并复制 V2 地图结构。</summary>
    public static DataMapObjectPbData FromPb(ProtocolV2.MapObjectPb value)
    {
        if (!Guid.TryParseExact(value.ObjectId, "N", out var id) || id == Guid.Empty || value.Revision <= 0 ||
            !float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new InvalidDataException("地图对象身份、版本或位置无效");
        return value.PayloadCase switch
        {
            ProtocolV2.MapObjectPb.PayloadOneofCase.Monster when !string.IsNullOrWhiteSpace(value.Monster.MonsterTypeId) =>
                new(id, value.Revision, value.X, value.Y, value.Monster.MonsterTypeId, 0, 0),
            ProtocolV2.MapObjectPb.PayloadOneofCase.GroundItem when value.GroundItem.ItemId > 0 && value.GroundItem.Count > 0 =>
                new(id, value.Revision, value.X, value.Y, null, value.GroundItem.ItemId, value.GroundItem.Count),
            _ => throw new InvalidDataException("地图对象载荷无效")
        };
    }
}
