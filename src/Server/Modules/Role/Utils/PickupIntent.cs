namespace Rpg.Server.Modules.Role.Utils;

/// <summary>检查当前拾取意图是否可以完成。</summary>
internal static class PickupIntent
{
    /// <summary>在当前目标一瓦片内安排实际地图校验；目标已到达同样可完成。</summary>
    internal static bool Ready(RoleContext context)
    {
        var cache = context.Cache;
        if (cache.Pickup is null) return false;
        var move = cache.Movement.Value;
        if (move.TargetX is not { } x || move.TargetY is not { } y) return true;
        var role = cache.Role.Value; var dx = role.X - x; var dy = role.Y - y;
        var tile = context.Config.Map.Items[role.MapId].TileSize;
        return (double)dx * dx + (double)dy * dy <= (double)tile * tile;
    }
}
