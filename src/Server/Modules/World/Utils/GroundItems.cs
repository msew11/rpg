using System;
using System.Numerics;
using Rpg.Protocol;
using Rpg.Server.Modules.World.Data;

namespace Rpg.Server.Modules.World.Utils;

/// <summary>共用地面物品的位置校验、变化标记和协议投影。</summary>
internal static class GroundItems
{
    /// <summary>检查来源权威位置属于当前地图。</summary>
    internal static Vector2 Position(WorldContext context, RoleWorldPositionPb position)
    {
        if (position is null || position.MapId != context.Cache.MapId || !GroundDrop.Contains(context.Cache.Layout, position.X, position.Y))
            throw new ArgumentException("地面操作位置无效");
        return new(position.X, position.Y);
    }
    /// <summary>静止观察者也必须收到所属块对象变化。</summary>
    internal static void Mark(WorldContext context, DataWorldObject item) => context.Cache.Roles.ObjectsChanged(item.X, item.Y);
    /// <summary>为每次回复创建独立对象。</summary>
    internal static MapObjectPb ToPb(DataWorldObject item)
    {
        var value = new MapObjectPb { ObjectId = item.ObjectId.ToString("N"), Revision = item.Revision, X = item.X, Y = item.Y };
        if (item.MonsterTypeId is { } type) value.Monster = new() { MonsterTypeId = type };
        else value.GroundItem = new() { ItemId = item.ItemId, Count = item.Count };
        return value;
    }
}
