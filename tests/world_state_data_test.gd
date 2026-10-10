## 验证世界状态的数据键映射、通知与副本隔离。
extends SceneTree


## 运行世界状态数据访问断言并退出。
func _initialize() -> void:
	var world := WorldState.new()
	var changes: Array = []
	world.value_changed.connect(func(key: StringName, value: Variant) -> void: changes.append([key, value]))
	var id := world.create_entity(Vector2.ZERO, 3.0)
	assert(world.ecs.has_entity(id))
	assert(world.ecs.get_component(id, PositionComponent).value == Vector2.ZERO)
	assert(world.has_value(WorldState.position_key(id)))
	assert(world.read_value(WorldState.position_key(id)) == Vector2.ZERO)
	var position_key := WorldState.position_key(id)
	var facing_key := WorldState.facing_key(id)
	var initial_changes := changes.size()
	assert(world.write_value(position_key, Vector2(2, 3)))
	assert(world.ecs.get_component(id, PositionComponent).value == Vector2(2, 3))
	assert(changes.size() == initial_changes + 1)
	assert(not world.write_value(position_key, Vector2(2, 3)))
	assert(changes.size() == initial_changes + 1)
	world.write_value(WorldState.intent_key(id), Vector2i(1, 0))
	var before_step := changes.size()
	world.step(1.0)
	assert(world.read_value(position_key) == world.ecs.get_component(id, PositionComponent).value)
	assert(world.read_value(facing_key) == Vector2i(1, 0))
	assert(changes.size() == before_step + 2)
	assert(changes[-2][0] == position_key or changes[-1][0] == position_key)
	assert(changes[-2][0] == facing_key or changes[-1][0] == facing_key)
	var chunk_key := WorldState.chunk_key(Vector2i(2, 3))
	var tiles := PackedByteArray([0, 1, 0])
	assert(world.write_value(chunk_key, tiles))
	var count_after_write := changes.size()
	tiles[0] = 1
	assert(world.read_value(chunk_key)[0] == 0)
	var read_tiles: PackedByteArray = world.read_value(chunk_key)
	read_tiles[0] = 1
	assert(world.read_value(chunk_key)[0] == 0)
	assert(world.write_value(chunk_key, read_tiles))
	assert(world.read_value(chunk_key)[0] == 1)
	assert(changes.size() == count_after_write + 1)
	assert(not world.write_value(chunk_key, read_tiles))
	print("world_state_data_test passed")
	quit()
