## 根据已加载区块数据绘制等轴地面和调试边缘。
class_name ChunkGround
extends Node2D

const GRASS := preload("res://assets/kenney_isometric_roads/grass.png")
const DIRT := preload("res://assets/kenney_isometric_roads/dirt.png")

var source: DataSource


## 绑定世界数据并订阅影响地面的变化。
func bind(data_source: DataSource) -> void:
	source = data_source
	source.value_changed.connect(_on_value_changed)
	queue_redraw()


## 在区块或边缘设置变化时请求重绘。
func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.LOADED_CHUNKS_KEY or key == WorldState.DEBUG_EDGES_KEY or String(key).begins_with("chunk/"):
		queue_redraw()


## 读取区块调试边缘的显示状态。
func debug_edges_enabled() -> bool:
	return source != null and source.read_value(WorldState.DEBUG_EDGES_KEY) == true


## 读取当前加载范围内的地块类型。
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


## 从区块数组读取指定地块的类型。
func _tile_type(tile_coord: Vector2i, tiles: PackedByteArray, chunk_size: Vector2i) -> int:
	var local := ChunkCoordinates.local_tile(tile_coord, chunk_size)
	return tiles[local.y * chunk_size.x + local.x]


## 绘制已加载地块及可选的区块边缘。
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
	if debug_edges_enabled():
		for raw: Vector2i in loaded:
			_draw_chunk_edge(raw, chunk_size)


## 沿区块四边绘制发光调试轮廓。
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
