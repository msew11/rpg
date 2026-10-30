using Rpg.Net;
using System.IO;
using Godot;
using Rpg.Net.Message;

namespace Rpg.Session.Data;

/// <summary>独立于协议对象的角色位置与移动确认快照。</summary>
public sealed record RoleWorldPositionPbData(long RoleId, int MapId, Vector2 Position, long Revision,
    long AcknowledgedAction)
{
    /// <summary>当前目标，空表示静止。</summary>
    public Vector2? Target { get; init; }
    /// <summary>服务器当前有效逻辑速度。</summary>
    public float Speed { get; init; }
    /// <summary>校验并复制协议位置。</summary>
    public static RoleWorldPositionPbData FromPb(RoleWorldPositionPb message)
    {
        if (message is null) throw new InvalidDataException("角色位置协议不能为空");
        var data = new RoleWorldPositionPbData(message.RoleId, message.MapId,
            new Vector2(message.X, message.Y), message.GetRevision(), message.AcknowledgedAction);
        data.Validate();
        return data;
    }

    /// <summary>直接验证并复制 V2 结构，身份来自完整状态根节点。</summary>
    public static RoleWorldPositionPbData FromPb(ProtocolV2.RoleWorldPositionPb message, long roleId)
    {
        if (message is null) throw new InvalidDataException("角色位置协议不能为空");
        var data = new RoleWorldPositionPbData(roleId, message.MapId,
            new Vector2(message.X, message.Y), 0, 0) { Target = message.Target is null ? null : new Vector2(message.Target.X, message.Target.Y), Speed = message.Speed };
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
            Revision < 0 || AcknowledgedAction < 0 || !float.IsFinite(Speed) || Speed < 0 ||
            (Target is { } target && (!float.IsFinite(target.X) || !float.IsFinite(target.Y))))
            throw new InvalidDataException("角色位置包含无效字段");
    }
}
