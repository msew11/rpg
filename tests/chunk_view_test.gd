extends SceneTree

const MAIN := preload("res://scenes/main.tscn")


func _initialize() -> void:
	call_deferred("run_test")


func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	assert(main.get_parent() == root)
	assert(_loaded_count(main) == 9)
	assert(main.debug_panel.borders_button.text.begins_with("F3"))
	assert(not main.debug_panel.borders_button.button_pressed)
	assert(main.debug_panel.radar_button.button_pressed)
	assert(main.radar.visible)
	assert(main.radar.chunk_corners(Vector2i(0, 0)).size() == 4)
	assert(main.radar.chunk_outline_color(Vector2i(0, 0)) == ChunkRadar.CURRENT_COLOR)
	assert(main.radar.chunk_outline_color(Vector2i(1, 0)) == ChunkRadar.LOADED_COLOR)
	assert(main.radar.chunk_outline_color(Vector2i(2, 0)) == ChunkRadar.UNLOADED_COLOR)
	assert(main.radar.wrap_east(Vector2i(-1, 0)))
	assert(main.radar.wrap_south(Vector2i(0, 3)))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	var initial_corner: Vector2 = main.radar.chunk_corners(Vector2i(0, 0))[0]
	_move_to(main, Vector2(6, 6))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.radar.chunk_corners(Vector2i(0, 0))[0] == initial_corner - Vector2(0, ChunkRadar.CHUNK_AXIS_PIXELS))
	assert(main.ground.displayed_tile_type(Vector2i(30, 1)) == -1)
	main.session.world.write_value(WorldState.facing_key(main.session.player_id), Vector2i(1, 0))
	assert(main.radar.facing_vector() == Vector2.RIGHT)
	_move_to(main, Vector2(6, 6) + IsometricProjection.to_world(Vector2.RIGHT))
	assert(main.radar.chunk_corners(Vector2i(0, 0))[0].x < initial_corner.x)
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(0, 0))[0].y, initial_corner.y - ChunkRadar.CHUNK_AXIS_PIXELS))
	_move_to(main, Vector2(11.9, 1))
	var before_border: Vector2 = main.radar.chunk_corners(Vector2i(1, 0))[0]
	_move_to(main, Vector2(12.1, 1))
	assert(main.radar.chunk_corners(Vector2i(1, 0))[0].x < before_border.x)
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(1, 0))[0].x, before_border.x - 0.2 / 12.0 * ChunkRadar.CHUNK_AXIS_PIXELS))
	assert(main.ground.displayed_tile_type(Vector2i(1, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(not main.ground.debug_edges_enabled())
	assert(main.radar.edge_hint_text() == "F3 BORDERS: OFF")
	var debug_key := InputEventKey.new()
	debug_key.keycode = KEY_F3
	debug_key.pressed = true
	main._unhandled_key_input(debug_key)
	assert(main.ground.debug_edges_enabled())
	assert(main.radar.edge_hint_text() == "F3 BORDERS: ON")
	assert(main.debug_panel.borders_button.button_pressed)
	main.debug_panel.radar_button.button_pressed = false
	assert(not main.radar.visible)
	main.debug_panel.radar_button.button_pressed = true
	assert(main.radar.visible)
	_move_to(main, Vector2(-1, -1))
	assert(main.radar.chunk_outline_color(Vector2i(-1, -1)) == ChunkRadar.CURRENT_COLOR)
	assert(Geometry2D.is_point_in_polygon(main.radar.player_marker_position(), main.radar.chunk_corners(Vector2i(-1, -1))))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.ground.displayed_tile_type(Vector2i(-1, -1)) == main.chunk_world.tile_type(Vector2i(-1, -1)))
	assert(main.ground.displayed_tile_type(Vector2i(25, 25)) == -1)
	main._unhandled_key_input(debug_key)
	assert(not main.ground.debug_edges_enabled())
	assert(main.radar.edge_hint_text() == "F3 BORDERS: OFF")
	assert(not main.debug_panel.borders_button.button_pressed)
	main.debug_panel.borders_button.button_pressed = true
	assert(main.ground.debug_edges_enabled())
	main.debug_panel.borders_button.button_pressed = false
	assert(not main.ground.debug_edges_enabled())
	_move_to(main, Vector2(49, 1))
	assert(main.radar.chunk_outline_color(Vector2i(4, 0)) == ChunkRadar.CURRENT_COLOR)
	assert(Geometry2D.is_point_in_polygon(main.radar.player_marker_position(), main.radar.chunk_corners(Vector2i(4, 0))))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	_move_to(main, Vector2(47.9, 1))
	var before_wrap: Vector2 = main.radar.chunk_corners(Vector2i(4, 0))[0]
	_move_to(main, Vector2(48.1, 1))
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(4, 0))[0].x, before_wrap.x - 0.2 / 12.0 * ChunkRadar.CHUNK_AXIS_PIXELS))
	_move_to(main, Vector2(49, 1))
	assert(main.ground.displayed_tile_type(Vector2i(49, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(main.camera.position == IsometricProjection.to_screen(Vector2(49, 1)))
	assert(main.radar.position == Vector2(16, 88))
	var map_key := InputEventKey.new()
	map_key.keycode = KEY_M
	map_key.pressed = true
	main._unhandled_key_input(map_key)
	assert(main.world_map.visible)
	assert(main.world_map.size.x > 0 and main.world_map.size.y > 0)
	for y in range(4):
		for x in range(4):
			assert(main.session.world.has_value(WorldState.chunk_key(Vector2i(x, y))))
	assert(main.world_map.chunk_corners(Vector2i(0, 0)).size() == 4)
	assert(main.world_map.player_marker_position() == main.world_map._map_point(Vector2(1, 1)))
	var before_pan: Vector2 = main.world_map.chunk_corners(Vector2i(0, 0))[0]
	var before_position: Vector2 = main.session.player_position()
	var mouse_down := InputEventMouseButton.new()
	mouse_down.button_index = MOUSE_BUTTON_LEFT
	mouse_down.pressed = true
	main.world_map._gui_input(mouse_down)
	var drag := InputEventMouseMotion.new()
	drag.relative = Vector2(23, -11)
	main.world_map._gui_input(drag)
	assert(main.world_map.chunk_corners(Vector2i(0, 0))[0] == before_pan + drag.relative)
	assert(main.session.player_position() == before_position)
	var mouse_up := InputEventMouseButton.new()
	mouse_up.button_index = MOUSE_BUTTON_LEFT
	mouse_up.pressed = false
	main.world_map._gui_input(mouse_up)
	main.world_map._gui_input(drag)
	assert(main.world_map.chunk_corners(Vector2i(0, 0))[0] == before_pan + drag.relative)
	main._unhandled_key_input(map_key)
	assert(not main.world_map.visible)
	assert(main.get_parent() == root)
	main.free()
	print("chunk_view_test passed")
	quit()


func _move_to(main: Node2D, position: Vector2) -> void:
	main.session.world.write_value(WorldState.position_key(main.session.player_id), position)
	main.chunk_world.load_around(position)
	main._sync_view()
	assert(_loaded_count(main) == 9)
	assert(main.player_view.position == IsometricProjection.to_screen(position))


func _loaded_count(main: Node2D) -> int:
	var loaded: Array = main.session.world.read_value(ChunkWorld.LOADED_KEY)
	return loaded.size()
