class_name ChunkWorld
extends RefCounted

const LOADED_KEY := WorldState.LOADED_CHUNKS_KEY
const LOAD_RADIUS := 1

var source: WorldState
var catalog: TestChunkCatalog
var _last_raw_chunk := Vector2i.ZERO
var _loaded_once := false


func _init(data_source: WorldState, test_catalog: TestChunkCatalog) -> void:
	source = data_source
	catalog = test_catalog
	source.write_value(WorldState.CHUNK_SIZE_KEY, catalog.chunk_size)
	source.write_value(WorldState.WORLD_SIZE_KEY, catalog.world_size)


func load_around(world_position: Vector2) -> void:
	var tile := ChunkCoordinates.tile_at(world_position)
	var center := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	if _loaded_once and center == _last_raw_chunk:
		return
	_last_raw_chunk = center
	_loaded_once = true
	var loaded: Array[Vector2i] = []
	for y in range(center.y - LOAD_RADIUS, center.y + LOAD_RADIUS + 1):
		for x in range(center.x - LOAD_RADIUS, center.x + LOAD_RADIUS + 1):
			var raw := Vector2i(x, y)
			ensure_chunk(ChunkCoordinates.canonical_chunk(raw, catalog.world_size))
			loaded.append(raw)
	source.write_value(LOADED_KEY, loaded)


func ensure_chunk(canonical_id: Vector2i) -> void:
	var key := WorldState.chunk_key(canonical_id)
	if not source.has_value(key):
		source.write_value(key, catalog.tiles_for(canonical_id))


func ensure_all_chunks() -> void:
	for y in range(catalog.world_size.y):
		for x in range(catalog.world_size.x):
			ensure_chunk(Vector2i(x, y))


func tile_type(tile: Vector2i) -> int:
	var raw := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	var canonical := ChunkCoordinates.canonical_chunk(raw, catalog.world_size)
	ensure_chunk(canonical)
	var tiles: PackedByteArray = source.read_value(WorldState.chunk_key(canonical))
	var local := ChunkCoordinates.local_tile(tile, catalog.chunk_size)
	return tiles[local.y * catalog.chunk_size.x + local.x]


func toggle_tile(world_position: Vector2) -> void:
	var tile := ChunkCoordinates.tile_at(world_position)
	var raw := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	var canonical := ChunkCoordinates.canonical_chunk(raw, catalog.world_size)
	ensure_chunk(canonical)
	var key := WorldState.chunk_key(canonical)
	var tiles: PackedByteArray = source.read_value(key)
	var local := ChunkCoordinates.local_tile(tile, catalog.chunk_size)
	var index := local.y * catalog.chunk_size.x + local.x
	tiles[index] = 1 - tiles[index]
	source.write_value(key, tiles)
