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
	assert(main.radar.chunk_rect(Vector2i(0, 0)).size == ChunkRadar.CELL_SIZE)
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	var initial_rect: Rect2 = main.radar.chunk_rect(Vector2i(0, 0))
	_move_to(main, Vector2(6, 6))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.radar.chunk_rect(Vector2i(0, 0)).position == initial_rect.position - ChunkRadar.CELL_SIZE / 2.0)
	assert(main.ground.displayed_tile_type(Vector2i(30, 1)) == -1)
	main.session.world.write_value(WorldState.facing_key(main.session.player_id), Vector2i(1, 0))
	assert(main.radar.facing_vector() == IsometricProjection.to_world(Vector2(1, 0)).normalized())
	_move_to(main, Vector2(11.9, 1))
	var before_border: Rect2 = main.radar.chunk_rect(Vector2i(1, 0))
	_move_to(main, Vector2(12.1, 1))
	assert(main.radar.chunk_rect(Vector2i(1, 0)).position.x < before_border.position.x)
	assert(is_equal_approx(main.radar.chunk_rect(Vector2i(1, 0)).position.x, before_border.position.x - 0.2 / 12.0 * ChunkRadar.CELL_SIZE.x))
	assert(main.ground.displayed_tile_type(Vector2i(1, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(not main.ground.debug_edges)
	var debug_key := InputEventKey.new()
	debug_key.keycode = KEY_F3
	debug_key.pressed = true
	main._unhandled_key_input(debug_key)
	assert(main.ground.debug_edges == OS.is_debug_build())
	_move_to(main, Vector2(-1, -1))
	var negative_rect: Rect2 = main.radar.chunk_rect(Vector2i(-1, -1))
	assert(negative_rect.has_point(main.radar.player_marker_position()))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.ground.displayed_tile_type(Vector2i(-1, -1)) == main.chunk_world.tile_type(Vector2i(-1, -1)))
	assert(main.ground.displayed_tile_type(Vector2i(25, 25)) == -1)
	main._unhandled_key_input(debug_key)
	assert(not main.ground.debug_edges)
	_move_to(main, Vector2(49, 1))
	var wrap_rect: Rect2 = main.radar.chunk_rect(Vector2i(4, 0))
	assert(wrap_rect.has_point(main.radar.player_marker_position()))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	_move_to(main, Vector2(47.9, 1))
	var before_wrap: Rect2 = main.radar.chunk_rect(Vector2i(4, 0))
	_move_to(main, Vector2(48.1, 1))
	assert(is_equal_approx(main.radar.chunk_rect(Vector2i(4, 0)).position.x, before_wrap.position.x - 0.2 / 12.0 * ChunkRadar.CELL_SIZE.x))
	_move_to(main, Vector2(49, 1))
	assert(main.ground.displayed_tile_type(Vector2i(49, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(main.camera.position == IsometricProjection.to_screen(Vector2(49, 1)))
	assert(main.radar.position == Vector2(16, 16))
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
