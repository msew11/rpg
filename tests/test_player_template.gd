## 验证 openspec/specs/player-visual-template/spec.md 的八方向动作、切换与重建场景。
class_name TestPlayerTemplate
extends SceneTree

## 玩家模板集成测试使用的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 等场景树就绪后验证角色显示行为。
func _initialize() -> void:
	call_deferred("run_test")


## 验证模板选择只改变显示，且动画和脚部锚点正确。
func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	assert(main.debug_panel.selected_player_template() == PlayerView.TEMPLATE_TALL)
	assert(main.radar.position == Vector2(16, 16))
	assert(not main.debug_panel.visible and main.debug_panel.gm_button.visible)
	assert(main.player_view.sprite.texture == PlayerView.TALL_IDLE)
	assert(main.player_view.sprite.scale == Vector2.ONE)
	assert(main.player_view.sprite.position == Vector2(-16, -32))
	var original_position: Vector2 = main.session.player_position()
	var original_facing: Vector2i = main.session.player_facing()
	for direction in [Vector2i.DOWN, Vector2i(1, 1), Vector2i.RIGHT, Vector2i(1, -1), Vector2i.UP, Vector2i(-1, -1), Vector2i.LEFT, Vector2i(-1, 1)]:
		main.player_view.facing = direction
		var expected_row := 0 if direction == Vector2i.DOWN else 4 if direction == Vector2i.UP else 2 if direction.y == 0 else 1 if direction.y > 0 else 3
		assert(main.player_view.sprite.region_rect.position.y == expected_row * PlayerView.TALL_FRAME)
		assert(main.player_view.sprite.flip_h == (direction.x < 0))
	main.player_view.moving = true
	main.player_view._process(PlayerView.FRAME_SECONDS)
	assert(main.player_view.sprite.texture == PlayerView.TALL_WALK)
	assert(main.player_view.sprite.region_rect.position.x == PlayerView.TALL_FRAME)
	main.player_view.moving = false
	assert(main.player_view.sprite.texture == PlayerView.TALL_IDLE)
	assert(main.player_view.sprite.region_rect.position.x == 0)
	main.debug_panel.player_template_selector.select(PlayerView.TEMPLATE_SMALL)
	main.debug_panel.player_template_selector.item_selected.emit(PlayerView.TEMPLATE_SMALL)
	assert(main.player_view.template_id == PlayerView.TEMPLATE_SMALL)
	assert(main.player_view.sprite.texture == PlayerView.SMALL_IDLE)
	assert(main.player_view.sprite.scale == Vector2(3, 3))
	assert(16.0 * main.player_view.sprite.scale.y >= UtilIsometricProjection.TILE_HALF_HEIGHT * 2.0 * 0.9)
	assert(main.player_view.sprite.position == Vector2(-36, -72))
	assert(main.player_view.sprite.position.y + main.player_view.sprite.region_rect.size.y * main.player_view.sprite.scale.y == 0.0)
	main.rebuild_player_view()
	assert(main.player_view.template_id == PlayerView.TEMPLATE_SMALL)
	main.start_new_game()
	assert(main.player_view.template_id == PlayerView.TEMPLATE_SMALL)
	assert(main.session.player_position() == original_position)
	assert(main.session.player_facing() == original_facing)
	main.debug_panel.player_template_selector.select(PlayerView.TEMPLATE_PLACEHOLDER)
	main.debug_panel.player_template_selector.item_selected.emit(PlayerView.TEMPLATE_PLACEHOLDER)
	assert(not main.player_view.sprite.visible)
	main.free()
	print("test_player_template passed")
	quit()
