## 根据已加载区块数据绘制等轴地面和调试边缘。
class_name ChunkGround
extends Node2D

## 草地测试地块的纹理。
const GRASS := preload("res://assets/kenney_isometric_roads/grass.png")
## 道路测试地块的纹理。
const DIRT := preload("res://assets/kenney_isometric_roads/dirt.png")

## 地面视图读取的世界状态。
var source: WorldManager


## 绑定世界数据并订阅影响地面的变化。
func bind(world: WorldManager) -> void:
	if source != null:
		source.chunks_changed.disconnect(_on_chunks_changed)
		source.debug_changed.disconnect(_on_debug_changed)
	source = world
	source.chunks_changed.connect(_on_chunks_changed)
	source.debug_changed.connect(_on_debug_changed)
	queue_redraw()


## 在区块数据或加载范围变化时请求重绘。
func _on_chunks_changed() -> void:
	queue_redraw()


## 在区块边缘开关变化时请求重绘。
func _on_debug_changed() -> void:
	queue_redraw()


## 读取区块调试边缘的显示状态。
func debug_edges_enabled() -> bool:
	return source != null and source.debug_view().edges


## 读取当前加载范围内的地块类型。
func displayed_tile_type(tile_coord: Vector2i) -> int:
	if source == null or source.chunks_view().loaded.is_empty():
		return -1
	var chunk_size: Vector2i = source.chunks_view().chunk_size
	var world_size: Vector2i = source.chunks_view().world_size
	var raw := UtilChunkCoordinates.raw_chunk(tile_coord, chunk_size)
	var loaded: Array[Vector2i] = source.chunks_view().loaded
	if not loaded.has(raw):
		return -1
	var canonical := UtilChunkCoordinates.canonical_chunk(raw, world_size)
	var tiles: PackedByteArray = source.chunks_view().tiles_for(canonical)
	return _tile_type(tile_coord, tiles, chunk_size)


## 从区块数组读取指定地块的类型。
func _tile_type(tile_coord: Vector2i, tiles: PackedByteArray, chunk_size: Vector2i) -> int:
	var local := UtilChunkCoordinates.local_tile(tile_coord, chunk_size)
	return tiles[local.y * chunk_size.x + local.x]


## 绘制已加载地块及可选的区块边缘。
func _draw() -> void:
	if source == null or source.chunks_view().loaded.is_empty():
		return
	var chunk_size: Vector2i = source.chunks_view().chunk_size
	var world_size: Vector2i = source.chunks_view().world_size
	var loaded: Array[Vector2i] = source.chunks_view().loaded
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
		var canonical := UtilChunkCoordinates.canonical_chunk(raw, world_size)
		by_raw[raw] = source.chunks_view().tiles_for(canonical)
	var min_tile := UtilChunkCoordinates.tile_origin(min_raw, chunk_size)
	var max_tile := UtilChunkCoordinates.tile_origin(max_raw + Vector2i.ONE, chunk_size) - Vector2i.ONE
	for row in range(min_tile.x + min_tile.y, max_tile.x + max_tile.y + 1):
		for x in range(min_tile.x, max_tile.x + 1):
			var y := row - x
			if y < min_tile.y or y > max_tile.y:
				continue
			var tile_coord := Vector2i(x, y)
			var raw := UtilChunkCoordinates.raw_chunk(tile_coord, chunk_size)
			if not by_raw.has(raw):
				continue
			var tiles: PackedByteArray = by_raw[raw]
			var tile_texture: Texture2D = DIRT if _tile_type(tile_coord, tiles, chunk_size) == 1 else GRASS
			draw_texture(tile_texture, UtilIsometricProjection.to_screen(Vector2(tile_coord)) - Vector2(50, 25))
	if debug_edges_enabled():
		for raw: Vector2i in loaded:
			_draw_chunk_edge(raw, chunk_size)


## 沿区块四边绘制发光调试轮廓。
func _draw_chunk_edge(raw: Vector2i, chunk_size: Vector2i) -> void:
	var origin := Vector2(UtilChunkCoordinates.tile_origin(raw, chunk_size)) - Vector2(0.5, 0.5)
	var corners := PackedVector2Array([
		UtilIsometricProjection.to_screen(origin),
		UtilIsometricProjection.to_screen(origin + Vector2(chunk_size.x, 0)),
		UtilIsometricProjection.to_screen(origin + Vector2(chunk_size)),
		UtilIsometricProjection.to_screen(origin + Vector2(0, chunk_size.y)),
		UtilIsometricProjection.to_screen(origin)
	])
	draw_polyline(corners, Color(0.1, 1.0, 0.95, 0.12), 16.0, true)
	draw_polyline(corners, Color(0.1, 1.0, 0.95, 0.28), 8.0, true)
	draw_polyline(corners, Color(0.72, 1.0, 0.96, 0.95), 2.0, true)
