class_name ChunkCoordinates
extends RefCounted


static func tile_at(world_position: Vector2) -> Vector2i:
	return Vector2i(floori(world_position.x), floori(world_position.y))


static func raw_chunk(tile: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(
		floori(float(tile.x) / chunk_size.x),
		floori(float(tile.y) / chunk_size.y)
	)


static func local_tile(tile: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(posmod(tile.x, chunk_size.x), posmod(tile.y, chunk_size.y))


static func canonical_chunk(raw: Vector2i, world_size: Vector2i) -> Vector2i:
	return Vector2i(posmod(raw.x, world_size.x), posmod(raw.y, world_size.y))


static func tile_origin(raw: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(raw.x * chunk_size.x, raw.y * chunk_size.y)
