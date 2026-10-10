class_name WorldMap
extends Control

const CHUNK_AXIS_PIXELS := 58.0
const GRASS_COLOR := Color(0.35, 0.56, 0.31)
const DIRT_COLOR := Color(0.68, 0.52, 0.35)
const LOADED_COLOR := Color(0.48, 0.78, 0.84)
const CURRENT_COLOR := Color(1.0, 0.82, 0.25)
const WRAP_COLOR := Color(0.95, 0.45, 0.95)

var source: DataSource
var player_id: int
var pan_offset := Vector2.ZERO
var _dragging := false


func bind(data_source: DataSource, entity_id: int) -> void:
	source = data_source
	player_id = entity_id
	source.value_changed.connect(_on_value_changed)
	queue_redraw()


func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.LOADED_CHUNKS_KEY or key == WorldState.position_key(player_id) or String(key).begins_with("chunk/"):
		queue_redraw()


func toggle_open() -> void:
	visible = not visible
	_dragging = false
	queue_redraw()


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		_dragging = event.pressed
		accept_event()
	elif event is InputEventMouseMotion and _dragging:
		pan_offset += event.relative
		queue_redraw()
		accept_event()


func _map_point(world_position: Vector2) -> Vector2:
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var relative := world_position / Vector2(chunk_size) - Vector2(world_size) / 2.0
	return size / 2.0 + pan_offset + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


func chunk_corners(canonical: Vector2i) -> PackedVector2Array:
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var origin := Vector2(ChunkCoordinates.tile_origin(canonical, chunk_size))
	return PackedVector2Array([
		_map_point(origin),
		_map_point(origin + Vector2(chunk_size.x, 0)),
		_map_point(origin + Vector2(chunk_size)),
		_map_point(origin + Vector2(0, chunk_size.y))
	])


func player_marker_position() -> Vector2:
	var position: Vector2 = source.read_value(WorldState.position_key(player_id))
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var period := Vector2(world_size * chunk_size)
	return _map_point(Vector2(fposmod(position.x, period.x), fposmod(position.y, period.y)))


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.04, 0.07, 0.09), true)
	if source == null:
		return
	var chunk_size: Vector2i = source.read_value(WorldState.CHUNK_SIZE_KEY)
	var world_size: Vector2i = source.read_value(WorldState.WORLD_SIZE_KEY)
	var loaded: Array = source.read_value(WorldState.LOADED_CHUNKS_KEY)
	var position: Vector2 = source.read_value(WorldState.position_key(player_id))
	var player_raw := ChunkCoordinates.raw_chunk(ChunkCoordinates.tile_at(position), chunk_size)
	var player_id_chunk := ChunkCoordinates.canonical_chunk(player_raw, world_size)
	var font := ThemeDB.fallback_font
	var loaded_ids: Array[Vector2i] = []
	for raw: Vector2i in loaded:
		var canonical := ChunkCoordinates.canonical_chunk(raw, world_size)
		if not loaded_ids.has(canonical):
			loaded_ids.append(canonical)
	draw_string(font, Vector2(24, 36), "WORLD MAP  ·  M close  ·  Left drag to pan", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)
	for y in range(world_size.y):
		for x in range(world_size.x):
			var canonical := Vector2i(x, y)
			var key := WorldState.chunk_key(canonical)
			if not source.has_value(key):
				continue
			var tiles: PackedByteArray = source.read_value(key)
			var origin := ChunkCoordinates.tile_origin(canonical, chunk_size)
			for local_y in range(chunk_size.y):
				for local_x in range(chunk_size.x):
					var tile := Vector2(origin + Vector2i(local_x, local_y))
					var corners := PackedVector2Array([
						_map_point(tile), _map_point(tile + Vector2.RIGHT),
						_map_point(tile + Vector2.ONE), _map_point(tile + Vector2.DOWN)
					])
					var kind: int = tiles[local_y * chunk_size.x + local_x]
					draw_colored_polygon(corners, DIRT_COLOR if kind == 1 else GRASS_COLOR)
	# Draw all borders after all tile fills, with loaded borders above gray ones.
	for draw_loaded in [false, true]:
		for y in range(world_size.y):
			for x in range(world_size.x):
				var canonical := Vector2i(x, y)
				if canonical == player_id_chunk or not source.has_value(WorldState.chunk_key(canonical)) or loaded_ids.has(canonical) != draw_loaded:
					continue
				var outline := chunk_corners(canonical)
				outline.append(outline[0])
				draw_polyline(outline, LOADED_COLOR if draw_loaded else Color(0.35, 0.4, 0.42), 2.0, true)
	for y in range(world_size.y):
		for x in range(world_size.x):
			var canonical := Vector2i(x, y)
			if not source.has_value(WorldState.chunk_key(canonical)):
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
