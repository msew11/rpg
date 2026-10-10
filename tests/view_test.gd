extends SceneTree

const MAIN := preload("res://scenes/main.tscn")


func _initialize() -> void:
	call_deferred("run_test")


func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	var original_id: int = main.session.player_id
	main.set_physics_process(false)
	main.session.set_player_direction(Vector2i(1, -1))
	main.session.step(1.0)
	main._sync_view()
	var expected_position := IsometricProjection.to_screen(main.session.player_position())
	assert(main.player_view.position == expected_position)
	assert(main.camera.position == expected_position)
	var original_view: Node2D = main.player_view
	main.rebuild_player_view()
	assert(main.player_view != original_view)
	assert(main.session.player_id == original_id)
	assert(main.player_view.position == expected_position)
	assert(main.player_view.facing == Vector2i(1, -1))
	assert(current_scene == null)
	main.free()
	print("view_test passed")
	quit()
