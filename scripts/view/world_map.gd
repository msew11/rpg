## 显示完整测试世界、玩家位置和区块状态。
class_name WorldMap
extends Control

const CHUNK_AXIS_PIXELS := UtilWorldMapProjection.CHUNK_AXIS_PIXELS
const GRASS_COLOR := Color(0.35, 0.56, 0.31)
const DIRT_COLOR := Color(0.68, 0.52, 0.35)
const LOADED_COLOR := Color(0.48, 0.78, 0.84)
const CURRENT_COLOR := Color(1.0, 0.82, 0.25)
const WRAP_COLOR := Color(0.95, 0.45, 0.95)

var source: WorldState
var player_id: int
var pan_offset := Vector2.ZERO
var _dragging := false


## 绑定玩家状态，并订阅世界变化。
func bind(world: WorldState, entity_id: int) -> void:
	source = world
	player_id = entity_id
	source.player_changed.connect(_on_player_changed)
	source.chunks_changed.connect(_on_chunks_changed)
	queue_redraw()


## 在玩家位置变化时请求重绘。
func _on_player_changed(entity_id: int) -> void:
	if entity_id == player_id:
		queue_redraw()


## 在区块数据或加载范围变化时刷新地图。
func _on_chunks_changed() -> void:
	queue_redraw()


## 切换地图显示并结束当前拖动。
func toggle_open() -> void:
	visible = not visible
	_dragging = false
	queue_redraw()


## 处理左键拖动以平移地图视图。
func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		_dragging = event.pressed
		accept_event()
	elif event is InputEventMouseMotion and _dragging:
		pan_offset += event.relative
		queue_redraw()
		accept_event()


## 将世界位置映射到可平移的地图坐标。
func _map_point(world_position: Vector2) -> Vector2:
	return UtilWorldMapProjection.map_point(source, size, pan_offset, world_position)


## 计算规范区块在地图上的四个顶点。
func chunk_corners(canonical: Vector2i) -> PackedVector2Array:
	return UtilWorldMapProjection.chunk_corners(source, size, pan_offset, canonical)


## 将玩家位置回环到有限地图后定位标记。
func player_marker_position() -> Vector2:
	return UtilWorldMapProjection.player_marker_position(source, player_id, size, pan_offset)


## 绘制全部测试地块、区块轮廓和玩家标记。
func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.04, 0.07, 0.09), true)
	if source == null:
		return
	var chunk_size := source.chunks().chunk_size
	var world_size := source.chunks().world_size
	var player_id_chunk := UtilWorldMapProjection.current_chunk(source, player_id)
	var font := ThemeDB.fallback_font
	draw_string(font, Vector2(24, 36), "WORLD MAP  ·  M close  ·  Left drag to pan", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)
	for y in range(world_size.y):
		for x in range(world_size.x):
			var canonical := Vector2i(x, y)
			if not source.chunks().has_chunk(canonical):
				continue
			var tiles := source.chunks().tiles_for(canonical)
			var origin := UtilChunkCoordinates.tile_origin(canonical, chunk_size)
			for local_y in range(chunk_size.y):
				for local_x in range(chunk_size.x):
					var tile := Vector2(origin + Vector2i(local_x, local_y))
					var corners := PackedVector2Array([
						_map_point(tile), _map_point(tile + Vector2.RIGHT),
						_map_point(tile + Vector2.ONE), _map_point(tile + Vector2.DOWN)
					])
					var kind: int = tiles[local_y * chunk_size.x + local_x]
					draw_colored_polygon(corners, DIRT_COLOR if kind == 1 else GRASS_COLOR)
	# 填充地块后统一绘制边框，让已加载区块的蓝色边框盖过灰色边框。
	for draw_loaded in [false, true]:
		for y in range(world_size.y):
			for x in range(world_size.x):
				var canonical := Vector2i(x, y)
				if canonical == player_id_chunk or not source.chunks().has_chunk(canonical) or UtilWorldMapProjection.is_loaded(source, canonical) != draw_loaded:
					continue
				var outline := chunk_corners(canonical)
				outline.append(outline[0])
				draw_polyline(outline, LOADED_COLOR if draw_loaded else Color(0.35, 0.4, 0.42), 2.0, true)
	for y in range(world_size.y):
		for x in range(world_size.x):
			var canonical := Vector2i(x, y)
			if not source.chunks().has_chunk(canonical):
				continue
			var corners := chunk_corners(canonical)
			var center := (corners[0] + corners[2]) * 0.5
			draw_string(font, center + Vector2(-14, 5), "%d,%d" % [x, y], HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color.WHITE)
	var outer := PackedVector2Array([
		_map_point(Vector2.ZERO),
		_map_point(Vector2(world_size.x * chunk_size.x, 0)),
		_map_point(Vector2(world_size * chunk_size)),
		_map_point(Vector2(0, world_size.y * chunk_size.y))
	])
	var outer_center := (outer[0] + outer[2]) * 0.5
	for i in range(outer.size()):
		var a := outer[i]
		var b := outer[(i + 1) % outer.size()]
		var outward := ((a + b) * 0.5 - outer_center).normalized() * 5.0
		draw_line(a + outward, b + outward, WRAP_COLOR, 3.0, true)
	var current_outline := chunk_corners(player_id_chunk)
	current_outline.append(current_outline[0])
	draw_polyline(current_outline, CURRENT_COLOR, 3.0, true)
	var marker := player_marker_position()
	draw_circle(marker, 8.0, Color(0.02, 0.05, 0.07))
	draw_circle(marker, 5.0, CURRENT_COLOR)
	draw_string(font, Vector2(24, size.y - 24), "PINK boundary loops  ·  BLUE loaded  ·  YELLOW current", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.82, 0.9, 0.92))
