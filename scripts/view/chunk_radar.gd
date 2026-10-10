class_name ChunkRadar
extends Control

const CELL_SIZE := Vector2(52, 52)
const GRID_VIEW := Rect2(16, 38, 208, 208)
const RADAR_CENTER := Vector2(120, 142)

var source: DataSource
var player_id: int


func bind(data_source: DataSource, entity_id: int) -> void:
	source = data_source
	player_id = entity_id
	source.value_changed.connect(_on_value_changed)
	queue_redraw()


func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.LOADED_CHUNKS_KEY or key == WorldState.position_key(player_id) or key == WorldState.facing_key(player_id):
		queue_redraw()


func chunk_rect(raw: Vector2i) -> Rect2:
	if source == null or not source.has_value(WorldState.LOADED_CHUNKS_KEY):
		return Rect2()
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	if not loaded.has(raw):
		return Rect2()
	var position: Vector2 = source.read_value(WorldState.position_key(player_id))
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var origin := Vector2(ChunkCoordinates.tile_origin(raw, chunk_size))
	return Rect2(RADAR_CENTER + (origin - position) / Vector2(chunk_size) * CELL_SIZE, CELL_SIZE)


func player_marker_position() -> Vector2:
	return RADAR_CENTER


func facing_vector() -> Vector2:
	var facing: Vector2i = source.read_value(WorldState.facing_key(player_id))
	return IsometricProjection.to_world(Vector2(facing)).normalized()


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
		var rect := chunk_rect(raw)
		var current := raw == player_raw
		var clipped := rect.intersection(GRID_VIEW)
		if not clipped.has_area():
			continue
		draw_rect(clipped, Color(0.16, 0.28, 0.32, 0.95) if current else Color(0.08, 0.15, 0.19, 0.85), true)
		_draw_chunk_outline(rect, Color(1.0, 0.82, 0.25) if current else Color(0.48, 0.78, 0.84))
		var canonical := ChunkCoordinates.canonical_chunk(raw, world_size)
		var label_position := rect.position + Vector2(6, 15)
		if not current and GRID_VIEW.has_point(label_position):
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


func _draw_chunk_outline(rect: Rect2, color: Color) -> void:
	var left := rect.position.x
	var right := rect.end.x
	var top := rect.position.y
	var bottom := rect.end.y
	if left >= GRID_VIEW.position.x and left <= GRID_VIEW.end.x:
		draw_line(Vector2(left, maxf(top, GRID_VIEW.position.y)), Vector2(left, minf(bottom, GRID_VIEW.end.y)), color, 2.0)
	if right >= GRID_VIEW.position.x and right <= GRID_VIEW.end.x:
		draw_line(Vector2(right, maxf(top, GRID_VIEW.position.y)), Vector2(right, minf(bottom, GRID_VIEW.end.y)), color, 2.0)
	if top >= GRID_VIEW.position.y and top <= GRID_VIEW.end.y:
		draw_line(Vector2(maxf(left, GRID_VIEW.position.x), top), Vector2(minf(right, GRID_VIEW.end.x), top), color, 2.0)
	if bottom >= GRID_VIEW.position.y and bottom <= GRID_VIEW.end.y:
		draw_line(Vector2(maxf(left, GRID_VIEW.position.x), bottom), Vector2(minf(right, GRID_VIEW.end.x), bottom), color, 2.0)
