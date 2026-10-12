## 验证 openspec/specs/world-interaction-events 的会话统一提交与重新绑定场景。
class_name TestSessionInteractions
extends SceneTree

## 用于验收界面与会话交互的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 延后执行需要挂入场景树的会话测试。
func _initialize() -> void:
	call_deferred("run_test")


## 验证当前会话的命令结果，以及新游戏后调试面板的目标世界。
func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	main.start_new_game()
	var old_session: LocalSession = main.session
	var old_world: WorldManager = old_session.world_manager
	assert(old_session.set_player_direction(Vector2i.RIGHT).changed)
	assert(not old_session.set_player_direction(Vector2i.RIGHT).changed)
	assert(old_session.set_player_direction(Vector2i.ZERO).changed)
	var tile_before: int = old_world.tile_type(Vector2i.ZERO)
	assert(old_session.toggle_player_tile().changed)
	assert(old_world.tile_type(Vector2i.ZERO) != tile_before)
	assert(old_session.set_debug_edges(true).changed)
	assert(not old_session.set_debug_edges(true).changed)
	assert(old_session.set_debug_radar(false).changed)
	assert(not old_world.debug().radar)
	assert(old_session.spawn_debug_monster().changed)
	assert(old_world.visible_monster_positions().size() == 1)
	assert(old_session.set_debug_edges(false).changed)

	main.start_new_game()
	assert(main.session != old_session)
	assert(main.debug_panel.interaction_session == main.session)
	assert(main.debug_panel.source == main.session.world_manager)
	main.debug_panel.borders_button.button_pressed = true
	assert(main.session.world_manager.debug().edges)
	assert(not old_world.debug().edges)
	var current_monsters: int = main.session.world_manager.visible_monster_positions().size()
	main.show_title()
	main.debug_panel.add_monster_button.pressed.emit()
	assert(main.session.world_manager.visible_monster_positions().size() == current_monsters)
	main.free()
	print("test_session_interactions passed")
	quit()
