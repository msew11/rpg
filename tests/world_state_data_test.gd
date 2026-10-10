extends SceneTree


func _initialize() -> void:
	var world := WorldState.new()
	var changes: Array = []
	world.value_changed.connect(func(key: StringName, value: Variant) -> void: changes.append([key, value]))
	var id := world.create_entity(Vector2.ZERO, 3.0)
	assert(world.has_value(WorldState.position_key(id)))
	assert(world.read_value(WorldState.position_key(id)) == Vector2.ZERO)
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
