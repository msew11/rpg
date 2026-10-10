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
