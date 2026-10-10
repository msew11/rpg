class_name ChunkRadar
extends Control

const GRID_VIEW := Rect2(16, 38, 208, 208)
const RADAR_CENTER := Vector2(120, 142)
const CHUNK_AXIS_PIXELS := 30.0

var source: DataSource
var player_id: int


func bind(data_source: DataSource, entity_id: int) -> void:
	source = data_source
	player_id = entity_id
	source.value_changed.connect(_on_value_changed)
	visible = source.read_value(WorldState.DEBUG_RADAR_KEY) == true
	queue_redraw()


func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.DEBUG_RADAR_KEY:
		visible = source.read_value(WorldState.DEBUG_RADAR_KEY) == true
	if key == WorldState.LOADED_CHUNKS_KEY or key == WorldState.DEBUG_EDGES_KEY or key == WorldState.position_key(player_id) or key == WorldState.facing_key(player_id):
		queue_redraw()


func edge_hint_text() -> String:
	return "F3 BORDERS: ON" if source != null and source.read_value(WorldState.DEBUG_EDGES_KEY) == true else "F3 BORDERS: OFF"


func player_marker_position() -> Vector2:
	return RADAR_CENTER


func facing_vector() -> Vector2:
	var facing: Vector2i = source.read_value(WorldState.facing_key(player_id))
	return Vector2(facing).normalized()


func chunk_corners(raw: Vector2i) -> PackedVector2Array:
	if source == null or not source.has_value(WorldState.LOADED_CHUNKS_KEY):
		return PackedVector2Array()
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	if not loaded.has(raw):
		return PackedVector2Array()
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var origin := Vector2(ChunkCoordinates.tile_origin(raw, chunk_size))
	return PackedVector2Array([
		_radar_point(origin),
		_radar_point(origin + Vector2(chunk_size.x, 0)),
		_radar_point(origin + Vector2(chunk_size)),
		_radar_point(origin + Vector2(0, chunk_size.y))
	])


func _radar_point(world_position: Vector2) -> Vector2:
	var player_position: Vector2 = source.read_value(WorldState.position_key(player_id))
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var relative := (world_position - player_position) / Vector2(chunk_size)
	return RADAR_CENTER + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.04, 0.08, 0.11, 0.9), true)
	if source == null or not source.has_value(WorldState.LOADED_CHUNKS_KEY):
		return
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	if loaded.is_empty():
		return
	var position: Vector2 = source.read_value(WorldState.position_key(player_id))
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var player_raw := ChunkCoordinates.raw_chunk(ChunkCoordinates.tile_at(position), chunk_size)
	var font := ThemeDB.fallback_font
	draw_string(font, Vector2(16, 25), "LOADED CHUNKS", HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color.WHITE)
	draw_rect(GRID_VIEW, Color(0.06, 0.11, 0.14), true)
	for raw: Vector2i in loaded:
		var corners := chunk_corners(raw)
		var current := raw == player_raw
		draw_colored_polygon(corners, Color(0.16, 0.28, 0.32, 0.95) if current else Color(0.08, 0.15, 0.19, 0.85))
		var outline := PackedVector2Array(corners)
		outline.append(corners[0])
		draw_polyline(outline, Color(1.0, 0.82, 0.25) if current else Color(0.48, 0.78, 0.84), 2.0, true)
		if not current:
			var canonical := ChunkCoordinates.canonical_chunk(raw, world_size)
			var label_position := (corners[0] + corners[2]) * 0.5 + Vector2(-12, 4)
			if GRID_VIEW.has_point(label_position):
				draw_string(font, label_position, "%d,%d" % [canonical.x, canonical.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.86, 0.94, 0.94))
	var marker := player_marker_position()
	var direction := facing_vector()
	var tip := marker + direction * 19.0
	var side := Vector2(-direction.y, direction.x)
	draw_line(marker, tip, Color(0.02, 0.05, 0.07), 6.0, true)
	draw_line(marker, tip, Color(1.0, 0.92, 0.35), 3.0, true)
	draw_colored_polygon(PackedVector2Array([tip + direction * 3.0, tip - direction * 8.0 + side * 5.0, tip - direction * 8.0 - side * 5.0]), Color(1.0, 0.92, 0.35))
	draw_circle(marker, 6.5, Color(0.02, 0.05, 0.07))
	draw_circle(marker, 4.5, Color(1.0, 0.92, 0.35))
	var current_id := ChunkCoordinates.canonical_chunk(player_raw, world_size)
	draw_string(font, Vector2(16, 265), "RAW %d,%d  ID %d,%d" % [player_raw.x, player_raw.y, current_id.x, current_id.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 0.92))
	draw_string(font, Vector2(16, 283), edge_hint_text(), HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(1.0, 0.85, 0.4))
