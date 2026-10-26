using Godot;
using Rpg.Config.Data;
using Rpg.World;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/ground-item-drop：客户端按 ECS 快照选择分散落点。</summary>
public sealed class GroundDropSelectionTests
{
    /// <summary>连续选点都满足角色周围合法圆环，并与已选物品保持投影间距。</summary>
    [Fact]
    public void ConsecutiveSelectionsUseVisibleGroundItems()
    {
        var map = MapUtils.CreateMap(new MapCfg { Width = 100, Height = 100, TileSize = 32 });
        var origin = new Vector2(1500, 1500);
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var first = Assert.IsType<Vector2>(WorldService.SelectGroundDropPosition(origin, map,
                new DataWorldObjectsState(1, new Dictionary<Guid, DataWorldObjectState>())));
            var id = Guid.NewGuid();
            var items = new Dictionary<Guid, DataWorldObjectState>
            {
                [id] = new(1, id, 1, first.X, first.Y, null, 2000, 1)
            };
            var second = Assert.IsType<Vector2>(WorldService.SelectGroundDropPosition(origin, map,
                new DataWorldObjectsState(2, items)));
            var delta = second - first;
            var projected = new Vector2((delta.X - delta.Y) * 0.5f, (delta.X + delta.Y) * 0.25f);
            Assert.True(projected.LengthSquared() >= 40f * 40f - 0.1f);
        }
    }

    /// <summary>地图角落仍选地图内合法位置，不从已有物品推断地图边界。</summary>
    [Fact]
    public void CornerSelectionRemainsInsideMap()
    {
        var map = MapUtils.CreateMap(new MapCfg { Width = 100, Height = 100, TileSize = 32 });
        var objects = new DataWorldObjectsState(1, new Dictionary<Guid, DataWorldObjectState>());
        foreach (var origin in new[] { new Vector2(0, 0), new Vector2(3199, 0),
                     new Vector2(0, 3199), new Vector2(3199, 3199) })
        {
            var point = Assert.IsType<Vector2>(WorldService.SelectGroundDropPosition(origin, map, objects));
            // 地面落点采用服务端连续坐标范围 [0, 像素边长)，不能把上界误写成整数像素。
            Assert.True(point.X >= 0 && point.X < 3200);
            Assert.True(point.Y >= 0 && point.Y < 3200);
        }
    }
}
