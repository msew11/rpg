extends SceneTree


func _initialize() -> void:
	var chunk_size := Vector2i(12, 12)
	var world_size := Vector2i(4, 4)
	assert(ChunkCoordinates.tile_at(Vector2(-0.1, -12.1)) == Vector2i(-1, -13))
	assert(ChunkCoordinates.raw_chunk(Vector2i(-1, -13), chunk_size) == Vector2i(-1, -2))
	assert(ChunkCoordinates.local_tile(Vector2i(-1, -13), chunk_size) == Vector2i(11, 11))
	assert(ChunkCoordinates.canonical_chunk(Vector2i(-1, -2), world_size) == Vector2i(3, 2))
	for x in range(-60, 61):
		for y in range(-60, 61):
			var tile := Vector2i(x, y)
			var raw := ChunkCoordinates.raw_chunk(tile, chunk_size)
			var local := ChunkCoordinates.local_tile(tile, chunk_size)
			assert(ChunkCoordinates.tile_origin(raw, chunk_size) + local == tile)
			assert(ChunkCoordinates.canonical_chunk(raw, world_size) == ChunkCoordinates.canonical_chunk(raw + world_size, world_size))
	print("chunk_coordinates_test passed")
	quit()
