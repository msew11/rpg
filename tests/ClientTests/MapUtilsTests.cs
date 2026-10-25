using Godot;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Session.Data;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 client-ecs-world 地图数据隔离及 client-config-map 地图边缘场景。</summary>
public sealed class MapUtilsTests
{
    /// <summary>已构造的地图不受原始配置覆盖集合的后续变化影响。</summary>
    [Fact]
    public void MapSnapshotKeepsSparseTilesIndependentOfConfiguration()
    {
        var config = new MapCfg
        {
            Width = 4, Height = 3, TileSize = 32, DefaultTile = "ground",
            Overrides = [new TileOverride { X = 1, Y = 2, Tile = "accent" }]
        };
        var map = MapUtils.CreateMap(config);
        config.Overrides.Clear();
        config.Overrides.Add(new TileOverride { X = 1, Y = 2, Tile = "changed" });
        Assert.Equal("accent", MapUtils.TileAt(map, 1, 2));
        Assert.Equal("ground", MapUtils.TileAt(map, 0, 0));
        Assert.Single(map.Overrides);
    }

    /// <summary>非方形地图的视口覆盖在左上及右下边缘均限制为有效格子。</summary>
    [Fact]
    public void VisibleRangeClipsBothMapEdges()
    {
        var map = MapUtils.CreateMap(new MapCfg { Width = 4, Height = 3, TileSize = 32 });
        Assert.Equal((0, 0, 1, 1), MapUtils.VisibleTiles(map, new Rect2(-32, -32, 64, 64)));
        Assert.Equal((3, 2, 3, 2), MapUtils.VisibleTiles(map, new Rect2(96, 64, 100, 100)));
        Assert.Equal((0, 0, 3, 2), MapUtils.VisibleTiles(map, new Rect2(-100, -100, 400, 400)));
    }

    /// <summary>移动及权威修正从世界实体读取地图，重新初始化后使用新地图边界。</summary>
    [Fact]
    public void WorldUsesCurrentMapDataForMovementAndAuthorityClamping()
    {
        var world = new WorldManager();
        var role = new RoleWorldStatePbData(1, "测试", 1, 10, 10, 10, 10, 0, 10,
            new Vector2(50, 50), RoleGender.Male, 0, 0);
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(
            new MapCfg { Width = 4, Height = 3, TileSize = 32 })));
        world.Enqueue(new InitializeRoleAction(role));
        world.Enqueue(new MoveAction(1, new Vector2(-10, 200)));
        world.Tick(0);
        Assert.Equal(new Vector2(0, 95), world.RoleTarget);
        world.Enqueue(new SyncRoleAction(role with
        {
            Position = new Vector2(200, -10), Revision = 1, PositionRevision = 1, AcknowledgedAction = 1
        }));
        world.Tick(0);
        Assert.Equal(new Vector2(127, 0), world.RolePosition);
        Assert.Equal(new Vector2(0, 95), world.RoleTarget);

        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(
            new MapCfg { Width = 2, Height = 2, TileSize = 32 })));
        world.Enqueue(new InitializeRoleAction(role));
        world.Enqueue(new MoveAction(2, new Vector2(200, 200)));
        world.Tick(0);
        Assert.Equal(new Vector2(63, 63), world.RoleTarget);
    }
}
