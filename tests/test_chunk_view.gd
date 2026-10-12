## 验证 openspec/specs/continuous-chunk-world/spec.md 的区块视图与快捷键，以及 openspec/specs/start-menu-and-save-slots/spec.md 的存档目录显示。
class_name TestChunkView
extends SceneTree

## 用于验证区块视图的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 延后执行需挂入场景树的视图测试。
func _initialize() -> void:
	call_deferred("run_test")


## 构建主场景并验证区块显示和地图交互。
func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	main.start_new_game()
	assert(main.get_parent() == root)
	assert(_loaded_count(main) == 9)
	assert(main.debug_panel.borders_button.text.begins_with("F3"))
	assert(not main.debug_panel.borders_button.button_pressed)
	assert(main.debug_panel.radar_button.button_pressed)
	assert(main.debug_panel.save_dir_path.text == ProjectSettings.globalize_path(SaveSlots.DEFAULT_DIR))
	assert(not main.debug_panel.save_dir_path.editable)
	assert(is_equal_approx(main.camera.zoom.x, 1.4))
	main.debug_panel.view_height_slider.value = 2.0
	assert(is_equal_approx(main.camera.zoom.x, 0.7))
	assert(is_equal_approx(main.camera.zoom.y, 0.7))
	assert(main.debug_panel.view_height_label.text.contains("2.00"))
	assert(_loaded_count(main) == 9)
	main.debug_panel.view_height_slider.value = 1.0
	assert(is_equal_approx(main.camera.zoom.x, 1.4))
	assert(main.radar.visible)
	assert(main.radar.chunk_corners(Vector2i(0, 0)).size() == 4)
	assert(main.radar.chunk_outline_color(Vector2i(0, 0)) == ChunkRadar.CURRENT_COLOR)
	assert(main.radar.chunk_outline_color(Vector2i(1, 0)) == ChunkRadar.LOADED_COLOR)
	assert(main.radar.chunk_outline_color(Vector2i(2, 0)) == ChunkRadar.UNLOADED_COLOR)
	assert(main.radar.wrap_east(Vector2i(-1, 0)))
	assert(main.radar.wrap_south(Vector2i(0, 3)))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.radar.visible_chunks().size() > 25)
	_assert_radar_coverage(main)
	assert(main.radar.visible_chunks().has(Vector2i(4, 0)))
	assert(main.radar.chunk_outline_color(Vector2i(4, 0)) == ChunkRadar.UNLOADED_COLOR)
	assert(main.radar.chunk_label(Vector2i(4, 0)) == "0,0")
	assert(main.radar.chunk_label(Vector2i(-1, -1)) == "3,3")
	var initial_corner: Vector2 = main.radar.chunk_corners(Vector2i(0, 0))[0]
	_move_to(main, Vector2(6, 6))
	_assert_radar_coverage(main)
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.radar.chunk_corners(Vector2i(0, 0))[0] == initial_corner - Vector2(0, ChunkRadar.CHUNK_AXIS_PIXELS))
	assert(main.ground.displayed_tile_type(Vector2i(30, 1)) == -1)
	WorldEcsTestAccess.SetPlayerFacing(main.session.world_manager, main.session.player_id, Vector2i(1, 0))
	assert(main.radar.facing_vector() == Vector2.RIGHT)
	_move_to(main, Vector2(6, 6) + UtilIsometricProjection.to_world(Vector2.RIGHT))
	assert(main.radar.chunk_corners(Vector2i(0, 0))[0].x < initial_corner.x)
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(0, 0))[0].y, initial_corner.y - ChunkRadar.CHUNK_AXIS_PIXELS))
	_move_to(main, Vector2(11.9, 1))
	var before_border: Vector2 = main.radar.chunk_corners(Vector2i(1, 0))[0]
	_move_to(main, Vector2(12.1, 1))
	assert(main.radar.chunk_corners(Vector2i(1, 0))[0].x < before_border.x)
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(1, 0))[0].x, before_border.x - 0.2 / 12.0 * ChunkRadar.CHUNK_AXIS_PIXELS))
	assert(main.ground.displayed_tile_type(Vector2i(1, 1)) == main.session.world_manager.tile_type(Vector2i(1, 1)))
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
	var tile_before_space: int = main.session.world_manager.tile_type(Vector2i(12, 1))
	main.debug_panel.radar_button.grab_focus()
	var space_key := InputEventKey.new()
	space_key.keycode = KEY_SPACE
	space_key.pressed = true
	main.get_viewport().push_input(space_key, true)
	assert(main.session.world_manager.tile_type(Vector2i(12, 1)) != tile_before_space)
	assert(main.debug_panel.radar_button.button_pressed)
	assert(main.debug_panel.borders_button.button_pressed)
	main.debug_panel.borders_button.grab_focus()
	main.get_viewport().push_input(space_key, true)
	assert(main.session.world_manager.tile_type(Vector2i(12, 1)) == tile_before_space)
	assert(main.debug_panel.radar_button.button_pressed)
	assert(main.debug_panel.borders_button.button_pressed)
	main.debug_panel.save_dir_path.grab_focus()
	main.get_viewport().push_input(space_key, true)
	assert(main.session.world_manager.tile_type(Vector2i(12, 1)) != tile_before_space)
	assert(main.debug_panel.radar_button.button_pressed)
	assert(main.debug_panel.borders_button.button_pressed)
	_move_to(main, Vector2(-1, -1))
	_assert_radar_coverage(main)
	assert(main.radar.visible_chunks().size() > 25)
	assert(main.radar.visible_chunks().has(Vector2i(-5, -1)))
	assert(main.radar.chunk_label(Vector2i(-5, -1)) == "3,3")
	assert(main.radar.chunk_outline_color(Vector2i(-1, -1)) == ChunkRadar.CURRENT_COLOR)
	assert(Geometry2D.is_point_in_polygon(main.radar.player_marker_position(), main.radar.chunk_corners(Vector2i(-1, -1))))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	assert(main.ground.displayed_tile_type(Vector2i(-1, -1)) == main.session.world_manager.tile_type(Vector2i(-1, -1)))
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
	_assert_radar_coverage(main)
	assert(main.radar.chunk_outline_color(Vector2i(4, 0)) == ChunkRadar.CURRENT_COLOR)
	assert(Geometry2D.is_point_in_polygon(main.radar.player_marker_position(), main.radar.chunk_corners(Vector2i(4, 0))))
	assert(main.radar.player_marker_position() == ChunkRadar.RADAR_CENTER)
	_move_to(main, Vector2(47.9, 1))
	var before_wrap: Vector2 = main.radar.chunk_corners(Vector2i(4, 0))[0]
	_move_to(main, Vector2(48.1, 1))
	assert(is_equal_approx(main.radar.chunk_corners(Vector2i(4, 0))[0].x, before_wrap.x - 0.2 / 12.0 * ChunkRadar.CHUNK_AXIS_PIXELS))
	_move_to(main, Vector2(49, 1))
	assert(main.ground.displayed_tile_type(Vector2i(49, 1)) == main.session.world_manager.tile_type(Vector2i(1, 1)))
	assert(main.camera.position == UtilIsometricProjection.to_screen(Vector2(49, 1)))
	assert(main.radar.position == Vector2(16, 312))
	var map_key := InputEventKey.new()
	map_key.keycode = KEY_M
	map_key.pressed = true
	main._unhandled_key_input(map_key)
	assert(main.world_map.visible)
	assert(main.world_map.size.x > 0 and main.world_map.size.y > 0)
	assert(main.session.world_manager.chunks_view().active_count() <= _loaded_count(main))
	assert(not main.session.world_manager.chunks_view().has_chunk(Vector2i(2, 2)))
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
	print("test_chunk_view passed")
	quit()


## 移动测试玩家并核对加载范围与视图位置。
func _move_to(main: Node2D, position: Vector2) -> void:
	WorldEcsTestAccess.SetPlayerPosition(main.session.world_manager, main.session.player_id, position)
	main.session.world_manager.step(0.0)
	main._sync_view()
	assert(_loaded_count(main) == 9)
	assert(main.player_view.position == UtilIsometricProjection.to_screen(position))


## 读取当前加载的行走区块数量。
func _loaded_count(main: Node2D) -> int:
	var loaded: Array[Vector2i] = main.session.world_manager.chunks_view().loaded
	return loaded.size()


## 检查雷达可见区块覆盖其视窗采样点。
func _assert_radar_coverage(main: Node2D) -> void:
	var radar: ChunkRadar = main.radar
	var visible := radar.visible_chunks()
	var chunk_size: Vector2i = main.session.world_manager.chunks_view().chunk_size
	var player_chunk_position: Vector2 = main.session.player_position() / Vector2(chunk_size)
	for screen_y in range(45, 307, 32):
		for screen_x in range(23, 285, 32):
			var offset := (Vector2(screen_x, screen_y) - ChunkRadar.RADAR_CENTER) / ChunkRadar.CHUNK_AXIS_PIXELS
			var chunk_position := player_chunk_position + Vector2((offset.x + offset.y) * 0.5, (offset.y - offset.x) * 0.5)
			assert(visible.has(Vector2i(floori(chunk_position.x), floori(chunk_position.y))))
