class_name ChunkGround
extends Node2D

const GRASS := preload("res://assets/kenney_isometric_roads/grass.png")
const DIRT := preload("res://assets/kenney_isometric_roads/dirt.png")

var source: DataSource
var debug_edges := false:
	set(value):
		debug_edges = value
		queue_redraw()


func bind(data_source: DataSource) -> void:
	source = data_source
	source.value_changed.connect(_on_value_changed)
	queue_redraw()


func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.LOADED_CHUNKS_KEY or String(key).begins_with("chunk/"):
		queue_redraw()


func displayed_tile_type(tile_coord: Vector2i) -> int:
	if source == null or not source.has_value(WorldState.LOADED_CHUNKS_KEY):
		return -1
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var raw := ChunkCoordinates.raw_chunk(tile_coord, chunk_size)
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	if not loaded.has(raw):
		return -1
	var canonical := ChunkCoordinates.canonical_chunk(raw, world_size)
	var tiles: PackedByteArray = source.read_value(WorldState.chunk_key(canonical))
	return _tile_type(tile_coord, tiles, chunk_size)


func _tile_type(tile_coord: Vector2i, tiles: PackedByteArray, chunk_size: Vector2i) -> int:
	var local := ChunkCoordinates.local_tile(tile_coord, chunk_size)
	return tiles[local.y * chunk_size.x + local.x]


func _draw() -> void:
	if source == null or not source.has_value(WorldState.LOADED_CHUNKS_KEY):
		return
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	if loaded.is_empty():
		return
	var by_raw: Dictionary = {}
	var min_raw: Vector2i = loaded[0]
	var max_raw: Vector2i = loaded[0]
	for raw: Vector2i in loaded:
		min_raw.x = mini(min_raw.x, raw.x)
		min_raw.y = mini(min_raw.y, raw.y)
		max_raw.x = maxi(max_raw.x, raw.x)
		max_raw.y = maxi(max_raw.y, raw.y)
		var canonical := ChunkCoordinates.canonical_chunk(raw, world_size)
		by_raw[raw] = source.read_value(WorldState.chunk_key(canonical))
	var min_tile := ChunkCoordinates.tile_origin(min_raw, chunk_size)
	var max_tile := ChunkCoordinates.tile_origin(max_raw + Vector2i.ONE, chunk_size) - Vector2i.ONE
	for row in range(min_tile.x + min_tile.y, max_tile.x + max_tile.y + 1):
		for x in range(min_tile.x, max_tile.x + 1):
			var y := row - x
			if y < min_tile.y or y > max_tile.y:
				continue
			var tile_coord := Vector2i(x, y)
			var raw := ChunkCoordinates.raw_chunk(tile_coord, chunk_size)
			if not by_raw.has(raw):
				continue
			var tiles: PackedByteArray = by_raw[raw]
			var tile_texture: Texture2D = DIRT if _tile_type(tile_coord, tiles, chunk_size) == 1 else GRASS
			draw_texture(tile_texture, IsometricProjection.to_screen(Vector2(tile_coord)) - Vector2(50, 25))
	if debug_edges and OS.is_debug_build():
		for raw: Vector2i in loaded:
			_draw_chunk_edge(raw, chunk_size)


func _draw_chunk_edge(raw: Vector2i, chunk_size: Vector2i) -> void:
	var origin := Vector2(ChunkCoordinates.tile_origin(raw, chunk_size)) - Vector2(0.5, 0.5)
	var corners := PackedVector2Array([
		IsometricProjection.to_screen(origin),
		IsometricProjection.to_screen(origin + Vector2(chunk_size.x, 0)),
		IsometricProjection.to_screen(origin + Vector2(chunk_size)),
		IsometricProjection.to_screen(origin + Vector2(0, chunk_size.y)),
		IsometricProjection.to_screen(origin)
	])
	draw_polyline(corners, Color(0.1, 1.0, 0.95, 0.12), 16.0, true)
	draw_polyline(corners, Color(0.1, 1.0, 0.95, 0.28), 8.0, true)
	draw_polyline(corners, Color(0.72, 1.0, 0.96, 0.95), 2.0, true)
