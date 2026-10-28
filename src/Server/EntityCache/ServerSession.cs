using System.Collections.Generic;
using System.Numerics;
using Rpg.Net.Message;

namespace Rpg.Server.EntityCache;

/// <summary>当前连接的非持久化模拟状态。</summary>
public sealed class ServerSession
{
    /// <summary>演示账号已经通过验证。</summary>
    public bool Authenticated { get; set; }
    /// <summary>当前角色，登录及列表阶段为空。</summary>
    public long? RoleId { get; set; }
    /// <summary>权威模拟位置。</summary>
    public Vector2 Position { get; set; }
    /// <summary>最近移动目标。</summary>
    public Vector2 Target { get; set; }
    /// <summary>最近接纳的移动序号。</summary>
    public long LastAction { get; set; }
    /// <summary>最近确认的移动序号。</summary>
    public long AcknowledgedAction { get; set; }
    /// <summary>最近发送私有物品快照的信封序号。</summary>
    public long ItemsRevision { get; set; }
    /// <summary>等待延迟确认的移动。</summary>
    internal List<PendingMove> Moves { get; } = [];
    /// <summary>等待走近的拾取意图。</summary>
    internal PickupItem? Pickup { get; set; }
    /// <summary>当前角色的范围观察者。</summary>
    internal MapAoiTracker? Aoi { get; set; }

    /// <summary>复制瞬态状态，地图回合不持有角色 Actor 的可写集合。</summary>
    internal ServerSession Copy()
    {
        var copy = new ServerSession
        {
            Authenticated = Authenticated, RoleId = RoleId, Position = Position, Target = Target,
            LastAction = LastAction, AcknowledgedAction = AcknowledgedAction, ItemsRevision = ItemsRevision,
            Pickup = Pickup?.Clone(), Aoi = Aoi?.Copy()
        };
        copy.Moves.AddRange(Moves);
        return copy;
    }

    /// <summary>离开角色时清理模拟状态，保留账号认证。</summary>
    public void Reset()
    {
        RoleId = null;
        Aoi = null;
        Moves.Clear();
        Pickup = null;
        LastAction = AcknowledgedAction = 0;
    }
}

/// <summary>一条待确认的移动及剩余模拟秒数。</summary>
/// <param name="ActionId">客户端意图序号。</param>
/// <param name="Remaining">距离确认的模拟秒数。</param>
internal sealed record PendingMove(long ActionId, double Remaining);
