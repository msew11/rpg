## 验证 openspec/specs/start-menu-and-save-slots/spec.md 的五槽位保存、恢复与无效档场景。
class_name TestSaveSlots
extends SceneTree

## 用于验证菜单交互的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 延后运行需要挂入场景树的存档测试。
func _initialize() -> void:
	call_deferred("run_test")


## 验证新游戏、独立槽位、跨会话恢复与损坏档隔离。
func run_test() -> void:
	var directory := "user://tests/save_slots_%d" % Time.get_ticks_usec()
	var store := SaveSlots.new(directory)
	var main := MAIN.instantiate()
	main.save_slots = store
	root.add_child(main)
	main.set_physics_process(false)
	assert(main.mode == Main.MODE_TITLE)
	assert(not main.game_buttons.visible)
	main.show_load_slots()
	assert(main.slot_list.get_child_count() == 5)
	assert(main.slot_list.get_child(0).disabled)
	assert(store.inspect(1)["state"] == "empty")
	assert(not store.save(0, main.session))
	assert(store.read(6).is_empty())

	main.start_new_game()
	var initial_tile: int = main.session.world.tile_type(Vector2i(0, 0))
	main.session.world.set_player_position(main.session.player_id, Vector2(27.25, -3.5))
	main.session.world.set_player_facing(main.session.player_id, Vector2i(-1, 1))
	main.session.world.toggle_tile(Vector2.ZERO)
	main.show_save_slots()
	assert(main.slot_list.get_child_count() == 5)
	main._on_slot_pressed(1)
	assert(store.inspect(1)["state"] == "valid")
	assert(store.inspect(5)["state"] == "empty")
	assert(not main.slot_list.get_child(0).disabled)

	main.session.world.set_player_position(main.session.player_id, Vector2(2, 4))
	main._on_slot_pressed(5)
	assert(store.inspect(5)["state"] == "valid")
	main.show_title()
	main.start_new_game()
	assert(main.session.player_position() == Vector2.ZERO)
	assert(main.session.world.tile_type(Vector2i(0, 0)) == initial_tile)
	main.show_title()
	main.show_load_slots()
	main._on_slot_pressed(1)
	assert(main.mode == Main.MODE_PLAYING)
	assert(main.session.player_position() == Vector2(27.25, -3.5))
	assert(main.session.player_facing() == Vector2i(-1, 1))
	assert(main.session.world.tile_type(Vector2i(0, 0)) != initial_tile)
	assert(main.camera.position == UtilIsometricProjection.to_screen(Vector2(27.25, -3.5)))
	main.show_title()
	main.show_load_slots()
	main._on_slot_pressed(5)
	assert(main.session.player_position() == Vector2(2, 4))

	var invalid_file := FileAccess.open("%s/slot_3.json" % directory, FileAccess.WRITE)
	invalid_file.store_string("{broken")
	invalid_file.close()
	assert(store.inspect(3)["state"] == "invalid")
	var incompatible := store.read(1)
	incompatible["world_size"] = [999, 999]
	invalid_file = FileAccess.open("%s/slot_4.json" % directory, FileAccess.WRITE)
	invalid_file.store_string(JSON.stringify(incompatible))
	invalid_file.close()
	assert(store.inspect(4)["state"] == "invalid")
	main.show_title()
	main.show_load_slots()
	assert(main.slot_list.get_child(2).disabled)
	assert(main.slot_list.get_child(3).disabled)
	var before_session: LocalSession = main.session
	main._on_slot_pressed(3)
	assert(main.session == before_session)
	assert(main.mode == Main.MODE_LOAD)

	main.free()
	for slot in [1, 3, 4, 5]:
		DirAccess.remove_absolute(ProjectSettings.globalize_path("%s/slot_%d.json" % [directory, slot]))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(directory))
	print("test_save_slots passed")
	quit()
