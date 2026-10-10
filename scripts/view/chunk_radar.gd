## 绘制以玩家为中心的区块雷达与回环提示。
class_name ChunkRadar
extends Control

const GRID_VIEW := RadarSystem.GRID_VIEW
const RADAR_CENTER := RadarSystem.RADAR_CENTER
const CHUNK_AXIS_PIXELS := RadarSystem.CHUNK_AXIS_PIXELS
const LOADED_COLOR := Color(0.48, 0.78, 0.84)
const UNLOADED_COLOR := Color(0.42, 0.45, 0.48)
const CURRENT_COLOR := Color(1.0, 0.82, 0.25)
const WRAP_COLOR := Color(0.95, 0.45, 0.95)

var source: WorldState
var player_id: int
var radar_system: RadarSystem


## 绑定玩家与雷达 System，并同步初始可见状态。
func bind(world: WorldState, entity_id: int) -> void:
	source = world
	player_id = entity_id
	radar_system = RadarSystem.new(source, player_id)
	source.ecs.add_system(radar_system)
	source.player_changed.connect(_on_player_changed)
	source.chunks_changed.connect(_on_chunks_changed)
	source.debug_changed.connect(_on_debug_changed)
	visible = source.debug().radar
	queue_redraw()


## 在玩家状态变化时刷新雷达。
func _on_player_changed(entity_id: int) -> void:
	if entity_id == player_id:
		radar_system.refresh()
		queue_redraw()


## 在加载范围变化时刷新雷达。
func _on_chunks_changed() -> void:
	radar_system.refresh()
	queue_redraw()


## 同步雷达可见状态与边缘提示。
func _on_debug_changed() -> void:
	visible = source.debug().radar
	queue_redraw()


## 返回区块边缘快捷键的状态提示。
func edge_hint_text() -> String:
	return "F3 BORDERS: ON" if source != null and source.debug().edges else "F3 BORDERS: OFF"


## 返回固定在雷达中心的玩家标记位置。
func player_marker_position() -> Vector2:
	return radar_system.player_marker_position()


## 读取玩家朝向并转为雷达方向向量。
func facing_vector() -> Vector2:
	return radar_system.facing_vector()


## 计算行走区块在雷达上的四个顶点。
func chunk_corners(raw: Vector2i) -> PackedVector2Array:
	return radar_system.chunk_corners(raw)


## 按当前、已加载或未加载状态选择轮廓颜色。
func chunk_outline_color(raw: Vector2i) -> Color:
	match radar_system.chunk_state(raw):
		2:
			return CURRENT_COLOR
		1:
			return LOADED_COLOR
		_:
			return UNLOADED_COLOR


## 判断区块东侧是否为世界回环接缝。
func wrap_east(raw: Vector2i) -> bool:
	return radar_system.wrap_east(raw)


## 判断区块南侧是否为世界回环接缝。
func wrap_south(raw: Vector2i) -> bool:
	return radar_system.wrap_south(raw)


## 避开玩家标记安排当前区块坐标标签。
func current_label_position() -> Vector2:
	return radar_system.current_label_position()


## 找出与雷达视窗相交的行走区块。
func visible_chunks() -> Array[Vector2i]:
	return radar_system.visible_chunks()


## 返回行走区块对应的规范坐标标签。
func chunk_label(raw: Vector2i) -> String:
	return radar_system.chunk_label(raw)


## 绘制区块网格、状态轮廓、接缝和玩家标记。
func _draw() -> void:
	var panel_color := Color(0.04, 0.08, 0.11, 0.9)
	draw_rect(Rect2(Vector2.ZERO, size), panel_color, true)
	if source == null or source.chunks().loaded.is_empty():
		return
	var player_raw := radar_system.current_raw_chunk()
	var font := ThemeDB.fallback_font
	var current_id := radar_system.current_canonical_chunk()
	draw_rect(GRID_VIEW, Color(0.06, 0.11, 0.14), true)
	var visible_raw := visible_chunks()
	for raw: Vector2i in visible_raw:
		var corners := chunk_corners(raw)
		var state := radar_system.chunk_state(raw)
		var current := state == 2
		var is_loaded := state == 1
		draw_colored_polygon(corners, Color(0.16, 0.28, 0.32, 0.95) if current else Color(0.08, 0.15, 0.19, 0.85) if is_loaded else Color(0.08, 0.10, 0.11, 0.85))
	# 填充后再画边框，已加载区块最后绘制以保持共用边为蓝色。
	for draw_loaded in [false, true]:
		for raw: Vector2i in visible_raw:
			if raw == player_raw or (radar_system.chunk_state(raw) == 1) != draw_loaded:
				continue
			var corners := chunk_corners(raw)
			var outline := PackedVector2Array(corners)
			outline.append(corners[0])
			draw_polyline(outline, LOADED_COLOR if draw_loaded else UNLOADED_COLOR, 2.0, true)
	for raw: Vector2i in visible_raw:
		if raw == player_raw:
			continue
		var corners := chunk_corners(raw)
		var is_loaded := radar_system.chunk_state(raw) == 1
		var label_position := (corners[0] + corners[2]) * 0.5 + Vector2(-12, 4)
		if GRID_VIEW.has_point(label_position):
			draw_string(font, label_position, chunk_label(raw), HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.86, 0.94, 0.94) if is_loaded else Color(0.66, 0.70, 0.71))
	for raw: Vector2i in visible_raw:
		var corners := chunk_corners(raw)
		if wrap_east(raw):
			_draw_wrap_line(corners[1], corners[2], corners)
		if wrap_south(raw):
			_draw_wrap_line(corners[2], corners[3], corners)
	var current_corners := chunk_corners(player_raw)
	var current_outline := PackedVector2Array(current_corners)
	current_outline.append(current_corners[0])
	draw_polyline(current_outline, CURRENT_COLOR, 3.0, true)
	draw_string(font, current_label_position(), "%d,%d" % [current_id.x, current_id.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 11, CURRENT_COLOR)
	# 绘制固定界面元素前，遮住雷达视窗外扩展的网格。
	var mask_color := Color(panel_color.r, panel_color.g, panel_color.b)
	draw_rect(Rect2(0, 0, size.x, GRID_VIEW.position.y), mask_color, true)
	draw_rect(Rect2(0, GRID_VIEW.end.y, size.x, size.y - GRID_VIEW.end.y), mask_color, true)
	draw_rect(Rect2(0, GRID_VIEW.position.y, GRID_VIEW.position.x, GRID_VIEW.size.y), mask_color, true)
	draw_rect(Rect2(GRID_VIEW.end.x, GRID_VIEW.position.y, size.x - GRID_VIEW.end.x, GRID_VIEW.size.y), mask_color, true)
	draw_string(font, Vector2(16, 25), "CHUNK %d,%d" % [current_id.x, current_id.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 14, CURRENT_COLOR)
	var marker := player_marker_position()
	var direction := facing_vector()
	var tip := marker + direction * 19.0
	var side := Vector2(-direction.y, direction.x)
	draw_line(marker, tip, Color(0.02, 0.05, 0.07), 6.0, true)
	draw_line(marker, tip, Color(1.0, 0.92, 0.35), 3.0, true)
	draw_colored_polygon(PackedVector2Array([tip + direction * 3.0, tip - direction * 8.0 + side * 5.0, tip - direction * 8.0 - side * 5.0]), Color(1.0, 0.92, 0.35))
	draw_circle(marker, 6.5, Color(0.02, 0.05, 0.07))
	draw_circle(marker, 4.5, Color(1.0, 0.92, 0.35))
	draw_string(font, Vector2(16, 329), "RAW %d,%d  ID %d,%d" % [player_raw.x, player_raw.y, current_id.x, current_id.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 0.92))
	draw_string(font, Vector2(16, 347), "YELLOW self  BLUE loaded", HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.8, 0.9, 0.92))
	draw_string(font, Vector2(16, 364), "GRAY unloaded  PINK loop", HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.8, 0.9, 0.92))


## 在区块边外侧绘制回环接缝标记。
func _draw_wrap_line(a: Vector2, b: Vector2, corners: PackedVector2Array) -> void:
	var center := (corners[0] + corners[2]) * 0.5
	var outward := ((a + b) * 0.5 - center).normalized() * 5.0
	draw_line(a + outward, b + outward, WRAP_COLOR, 2.0, true)
