## 验证 openspec/specs/world-script-boundaries/spec.md 的只读世界查询与变化通知场景。
class_name TestWorldManagerData
extends SceneTree


## 运行 ECS 世界状态断言并退出。
func _initialize() -> void:
	var cache := LocalDataRepoCache.new()
	var world: WorldManager = WorldManager.Create(cache)
	var ecs: EcsWorld = WorldEcsTestAccess.FromCache(cache)
	assert(ecs.HasEntity(world.world_entity_id))
	assert(world.chunks_view() is WorldChunksView)
	assert(world.debug_view() is WorldDebugView)
	var player_changes: Array[int] = []
	var chunk_changes: Array[int] = []
	var debug_changes: Array[int] = []
	var on_player_changed := func(id: int) -> void: player_changes.append(id)
	var on_chunks_changed := func() -> void: chunk_changes.append(1)
	var on_debug_changed := func() -> void: debug_changes.append(1)
	world.player_changed.connect(on_player_changed)
	world.chunks_changed.connect(on_chunks_changed)
	world.debug_changed.connect(on_debug_changed)
	var id := WorldEcsTestAccess.CreatePlayer(world, Vector2.ZERO, 3.0)
	assert(id != world.world_entity_id)
	assert(world.player_position(id) == Vector2.ZERO)
	assert(ecs.HasEntity(id))
	assert(player_changes == [id])
	WorldEcsTestAccess.SetPlayerPosition(world, id, Vector2(2, 3))
	assert(world.player_position(id) == Vector2(2, 3))
	assert(player_changes.size() == 2)
	WorldEcsTestAccess.SetPlayerPosition(world, id, Vector2(2, 3))
	assert(player_changes.size() == 2)
	world.send_event(MoveIntentEvent.Create(id, Vector2i(1, 0)))
	world.step(1.0)
	assert(world.player_position(id).is_equal_approx(Vector2(2, 3) + UtilIsometricProjection.movement_direction(Vector2i(1, 0)) * 3.0))
	assert(world.player_facing(id) == Vector2i(1, 0))
	assert(player_changes.size() == 3)
	world.send_event(SetDebugEdgesEvent.Create(true))
	world.send_event(SetDebugEdgesEvent.Create(true))
	assert(world.debug_view().edges and debug_changes.size() == 1)
	var canonical := Vector2i(2, 3)
	var tiles := PackedByteArray([0, 1, 0])
	assert(WorldEcsTestAccess.SetChunkTiles(world, canonical, tiles))
	assert(chunk_changes.size() == 1)
	tiles[0] = 1
	assert(world.chunks_view().tiles_for(canonical)[0] == 0)
	var read_tiles: PackedByteArray = world.chunks_view().tiles_for(canonical)
	read_tiles[0] = 1
	assert(world.chunks_view().tiles_for(canonical)[0] == 0)
	assert(WorldEcsTestAccess.SetChunkTiles(world, canonical, read_tiles))
	assert(world.chunks_view().tiles_for(canonical)[0] == 1)
	assert(chunk_changes.size() == 2)
	assert(not WorldEcsTestAccess.SetChunkTiles(world, canonical, read_tiles))
	assert(chunk_changes.size() == 2)
	WorldEcsTestAccess.InitializeTestChunks(world, ManualChunkCfg.new())
	var loaded_count: int = world.chunks_view().loaded.size()
	var notified_count: int = chunk_changes.size()
	var loaded_copy: Array[Vector2i] = world.chunks_view().loaded
	loaded_copy.clear()
	assert(world.chunks_view().loaded.size() == loaded_count)
	assert(chunk_changes.size() == notified_count)
	world.player_changed.disconnect(on_player_changed)
	world.chunks_changed.disconnect(on_chunks_changed)
	world.debug_changed.disconnect(on_debug_changed)
	print("test_world_manager_data passed")
	quit()
