## 验证世界实体的区块组件、玩家组件及变化通知。
extends SceneTree


## 运行 ECS 世界状态断言并退出。
func _initialize() -> void:
	var world := WorldState.new()
	assert(world.ecs.has_entity(world.world_entity_id))
	assert(world.ecs.get_component(world.world_entity_id, WorldChunksComponent) == world.chunks())
	assert(world.ecs.get_component(world.world_entity_id, WorldDebugComponent) == world.debug())
	var player_changes: Array[int] = []
	var chunk_changes: Array[int] = []
	var debug_changes: Array[int] = []
	world.player_changed.connect(func(id: int) -> void: player_changes.append(id))
	world.chunks_changed.connect(func() -> void: chunk_changes.append(1))
	world.debug_changed.connect(func() -> void: debug_changes.append(1))
	var id := world.create_player(Vector2.ZERO, 3.0)
	assert(id != world.world_entity_id)
	assert(world.player_position(id) == Vector2.ZERO)
	assert(world.ecs.get_component(id, PositionComponent).value == Vector2.ZERO)
	assert(player_changes == [id])
	world.set_player_position(id, Vector2(2, 3))
	assert(world.player_position(id) == Vector2(2, 3))
	assert(player_changes.size() == 2)
	world.set_player_position(id, Vector2(2, 3))
	assert(player_changes.size() == 2)
	world.set_player_intent(id, Vector2i(1, 0))
	world.step(1.0)
	assert(world.player_position(id) == world.ecs.get_component(id, PositionComponent).value)
	assert(world.player_facing(id) == Vector2i(1, 0))
	assert(player_changes.size() == 3)
	world.set_debug_edges(true)
	world.set_debug_edges(true)
	assert(world.debug().edges and debug_changes.size() == 1)
	var canonical := Vector2i(2, 3)
	var tiles := PackedByteArray([0, 1, 0])
	assert(world.set_chunk_tiles(canonical, tiles))
	assert(chunk_changes.size() == 1)
	tiles[0] = 1
	assert(world.chunks().tiles_for(canonical)[0] == 0)
	var read_tiles := world.chunks().tiles_for(canonical)
	read_tiles[0] = 1
	assert(world.chunks().tiles_for(canonical)[0] == 0)
	assert(world.set_chunk_tiles(canonical, read_tiles))
	assert(world.chunks().tiles_for(canonical)[0] == 1)
	assert(chunk_changes.size() == 2)
	assert(not world.set_chunk_tiles(canonical, read_tiles))
	assert(chunk_changes.size() == 2)
	print("world_state_data_test passed")
	quit()
