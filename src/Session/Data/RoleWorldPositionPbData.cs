using Rpg.Net;
using System.IO;
using Godot;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>独立于协议对象的角色位置与移动确认快照。</summary>
public sealed record RoleWorldPositionPbData(long RoleId, int MapId, Vector2 Position, long Revision,
    long AcknowledgedAction)
{
    /// <summary>校验并复制协议位置。</summary>
    public static RoleWorldPositionPbData FromPb(RoleWorldPositionPb message)
    {
        if (message is null) throw new InvalidDataException("角色位置协议不能为空");
        var data = new RoleWorldPositionPbData(message.RoleId, message.MapId,
            new Vector2(message.X, message.Y), message.GetRevision(), message.AcknowledgedAction);
        data.Validate();
        return data;
    }

    /// <summary>创建新的角色位置协议对象。</summary>
    public RoleWorldPositionPb ToPb()
    {
        Validate();
        return new RoleWorldPositionPb { RoleId = RoleId, MapId = MapId, X = Position.X, Y = Position.Y,
            AcknowledgedAction = AcknowledgedAction }.WithRevision(Revision);
    }

    /// <summary>拒绝非法位置和版本。</summary>
    private void Validate()
    {
        if (RoleId <= 0 || MapId <= 0 || !float.IsFinite(Position.X) || !float.IsFinite(Position.Y) ||
            Revision < 0 || AcknowledgedAction < 0)
            throw new InvalidDataException("角色位置包含无效字段");
    }
}
