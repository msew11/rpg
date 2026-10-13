## 验证 openspec/specs/gm-panel-ui/spec.md、player-visual-template/spec.md 与 continuous-chunk-world/spec.md 的 GM 交互。
class_name TestGmPanel
extends SceneTree

## 用于 UI 验收的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 在场景树就绪后运行交互测试。
func _initialize() -> void:
	call_deferred("run_test")


## 验证展开、固定、拖动、分组、动作和滚轮视野。
func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	main.start_new_game()
	var panel: DebugPanel = main.debug_panel
	assert(not panel.visible and panel.gm_button.visible)
	assert(main.radar.visible and main.radar.position == Vector2(16, 16))
	assert(panel.tabs.get_tab_count() == 3)
	var press := InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	press.global_position = panel.gm_button.global_position + Vector2(12, 12)
	panel._on_gm_button_input(press)
	var release := InputEventMouseButton.new()
	release.button_index = MOUSE_BUTTON_LEFT
	release.global_position = press.global_position
	panel._input(release)
	assert(panel.visible and not panel.gm_button.visible)
	var outside := InputEventMouseButton.new()
	outside.button_index = MOUSE_BUTTON_LEFT
	outside.pressed = true
	outside.global_position = Vector2(900, 600)
	panel._input(outside)
	assert(not panel.visible and panel.gm_button.visible)
	var start_position := panel.gm_button.position
	panel._on_gm_button_input(press)
	var motion := InputEventMouseMotion.new()
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	motion.global_position = press.global_position + Vector2(40, 20)
	panel._input(motion)
	assert(panel.gm_button.position == start_position + Vector2(40, 20))
	release.global_position = motion.global_position
	panel._input(release)
	assert(not panel.visible)
	panel.open_panel()
	panel.pin_button.button_pressed = true
	panel._input(outside)
	assert(panel.visible)
	var panel_position := panel.position
	press.global_position = panel.drag_handle.global_position + Vector2(10, 10)
	panel._on_drag_handle_input(press)
	motion.global_position = press.global_position + Vector2(35, 25)
	panel._input(motion)
	assert(panel.position == panel_position + Vector2(35, 25))
	panel._input(release)
	panel.tabs.current_tab = 1
	panel.action_selector.select(PlayerView.ACTION_ATTACK)
	panel.action_selector.item_selected.emit(PlayerView.ACTION_ATTACK)
	assert(main.player_view.demo_action == PlayerView.ACTION_ATTACK)
	assert(main.player_view.sprite.texture == PlayerView.TALL_ATTACK)
	main.player_view._process(PlayerView.FRAME_SECONDS * 5.0)
	assert(main.player_view.frame_index == 5)
	main.player_view.moving = true
	assert(main.player_view.demo_action == PlayerView.ACTION_DEFAULT)
	assert(panel.action_selector.get_selected_id() == PlayerView.ACTION_DEFAULT)
	assert(main.player_view.sprite.texture == PlayerView.TALL_WALK)
	main.player_view.moving = false
	panel.action_selector.select(PlayerView.ACTION_ATTACK)
	panel.action_selector.item_selected.emit(PlayerView.ACTION_ATTACK)
	panel.player_template_selector.select(PlayerView.TEMPLATE_SMALL)
	panel.player_template_selector.item_selected.emit(PlayerView.TEMPLATE_SMALL)
	assert(panel.action_selector.get_selected_id() == PlayerView.ACTION_DEFAULT)
	assert(main.player_view.demo_action == PlayerView.ACTION_DEFAULT)
	assert(main.player_view.sprite.texture == PlayerView.SMALL_IDLE)
	panel.player_template_selector.select(PlayerView.TEMPLATE_PLACEHOLDER)
	panel.player_template_selector.item_selected.emit(PlayerView.TEMPLATE_PLACEHOLDER)
	assert(panel.action_selector.disabled)
	assert(main.player_view.demo_action == PlayerView.ACTION_DEFAULT)
	panel.pin_button.button_pressed = false
	panel._input(outside)
	assert(not panel.visible and main.radar.visible)
	var wheel := InputEventMouseButton.new()
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	main._unhandled_input(wheel)
	assert(is_equal_approx(panel.view_height_slider.value, 1.25))
	assert(is_equal_approx(main.camera.zoom.x, Main.DEFAULT_CAMERA_ZOOM / 1.25))
	main.free()
	print("test_gm_panel passed")
	quit()
