using Rpg.Config.Data;
using Rpg.Protocol;
using Rpg.Session.Data;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 client-ecs-world 动态 AOI 对象的身份、载荷和移除契约。</summary>
[Trait("Runtime", "Godot")]
public sealed class WorldMapEntityTests
{
    /// <summary>同一 GUID 更新保持 entity，完整重同步移除缺席对象。</summary>
    [Fact]
    public void AoiSnapshotsReconcileStableEntities()
    {
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(new MapCfg { Width = 2, Height = 2, TileSize = 32 })));
        var itemId = Guid.NewGuid();
        var monsterId = Guid.NewGuid();
        world.Enqueue(new SyncMapObjectsAction(Snapshot(1,
            new MapObjectPb { ObjectId = itemId.ToString("N"), Revision = 1, X = 8, Y = 9,
                GroundItem = new() { ItemId = 1012, Count = 2 } },
            new MapObjectPb { ObjectId = monsterId.ToString("N"), Revision = 1, X = 12, Y = 13,
                Monster = new() { MonsterTypeId = "test" } })));
        world.Tick(0);
        var first = world.MapObjects!;
        Assert.Equal(2, first.Objects.Count);
        Assert.NotEqual(first.Objects[itemId].EntityId, first.Objects[monsterId].EntityId);
        world.Enqueue(new SyncMapObjectsAction(Snapshot(2,
            new MapObjectPb { ObjectId = itemId.ToString("N"), Revision = 2, X = 10, Y = 11,
                GroundItem = new() { ItemId = 1012, Count = 3 } })));
        world.Tick(0);
        Assert.Equal(first.Objects[itemId].EntityId, world.MapObjects!.Objects[itemId].EntityId);
        Assert.Equal(3, world.MapObjects.Objects[itemId].Count);
        Assert.Equal(10, world.MapObjects.Objects[itemId].X);
        Assert.False(world.MapObjects.Objects.ContainsKey(monsterId));
        world.Enqueue(new SyncMapObjectsAction(null));
        world.Tick(0);
        Assert.Null(world.MapObjects);
    }

    /// <summary>建立单块地图的合法完整 AOI 快照。</summary>
    private static DataMapAoiSnapshotPbData Snapshot(long sequence, params MapObjectPb[] objects)
    {
        var chunk = new MapChunkPb { Coordinate = new() { X = 0, Y = 0 } };
        chunk.Objects.AddRange(objects);
        var snapshot = new MapAoiSnapshotPb { MapId = 1,
            Layout = new() { Width = 2, Height = 2, TileSize = 32, ChunkSizeTiles = 2 },
            Center = new() { X = 0, Y = 0 } };
        snapshot.Chunks.Add(chunk);
        return DataMapAoiSnapshotPbData.FromPb(snapshot, 1);
    }
}
