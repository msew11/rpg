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
	assert(main.ground.displayed_tile_type(Vector2i(1, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(main.ground.displayed_tile_type(Vector2i(30, 1)) == -1)
	assert(not main.ground.debug_edges)
	var debug_key := InputEventKey.new()
	debug_key.keycode = KEY_F3
	debug_key.pressed = true
	main._unhandled_key_input(debug_key)
	assert(main.ground.debug_edges == OS.is_debug_build())
	_move_to(main, Vector2(-1, -1))
	assert(main.ground.displayed_tile_type(Vector2i(-1, -1)) == main.chunk_world.tile_type(Vector2i(-1, -1)))
	assert(main.ground.displayed_tile_type(Vector2i(25, 25)) == -1)
	main._unhandled_key_input(debug_key)
	assert(not main.ground.debug_edges)
	_move_to(main, Vector2(49, 1))
	assert(main.ground.displayed_tile_type(Vector2i(49, 1)) == main.chunk_world.tile_type(Vector2i(1, 1)))
	assert(main.camera.position == IsometricProjection.to_screen(Vector2(49, 1)))
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
