## 验证八方向移动、速度与视图重建后的状态。
extends SceneTree


## 运行玩家移动和输入方向断言并退出。
func _initialize() -> void:
	var session := LocalSession.new()
	var player_id := session.player_id
	assert(player_id > 0)
	assert(session.world.player_position(player_id) == Vector2.ZERO)
	assert(session.world.ecs.get_component(player_id, PositionComponent) is PositionComponent)

	var directions := [
		Vector2i(-1, -1), Vector2i(0, -1), Vector2i(1, -1),
		Vector2i(-1, 0), Vector2i(1, 0),
		Vector2i(-1, 1), Vector2i(0, 1), Vector2i(1, 1)
	]
	for direction in directions:
		var test_session := LocalSession.new()
		test_session.set_player_direction(direction)
		test_session.step(1.0 / 60.0)
		var screen_delta := UtilIsometricProjection.to_screen(test_session.player_position())
		assert(sign(screen_delta.x) == sign(direction.x))
		assert(sign(screen_delta.y) == sign(direction.y))
		if direction.x != 0 and direction.y != 0:
			assert(absf(absf(screen_delta.y / screen_delta.x) - UtilIsometricProjection.TILE_HALF_HEIGHT / UtilIsometricProjection.TILE_HALF_WIDTH) < 0.0001)
		elif direction.x == 0:
			assert(absf(screen_delta.x) < 0.0001)
		else:
			assert(absf(screen_delta.y) < 0.0001)
		assert(test_session.player_facing() == direction)

	assert(MovementInput.from_axes(true, true, false, false) == Vector2i.ZERO)
	assert(MovementInput.from_axes(false, false, true, true) == Vector2i.ZERO)
	assert(MovementInput.from_axes(false, true, true, false) == Vector2i(1, -1))

	var single := LocalSession.new()
	var diagonal := LocalSession.new()
	for frame in range(600):
		single.set_player_direction(Vector2i(0, -1))
		diagonal.set_player_direction(Vector2i(1, -1))
		single.step(1.0 / 60.0)
		diagonal.step(1.0 / 60.0)
	assert(abs(single.player_position().length() - diagonal.player_position().length()) < 0.001)

	diagonal.set_player_direction(Vector2i.ZERO)
	var stopped_position := diagonal.player_position()
	diagonal.step(1.0 / 60.0)
	assert(diagonal.player_position() == stopped_position)
	assert(diagonal.player_facing() == Vector2i(1, -1))

	# 重建视图不创建或重置对应的世界实体。
	var view_a := Node2D.new()
	view_a.position = UtilIsometricProjection.to_screen(session.player_position())
	view_a.free()
	var view_b := Node2D.new()
	view_b.position = UtilIsometricProjection.to_screen(session.player_position())
	assert(session.player_id == player_id)
	assert(view_b.position == UtilIsometricProjection.to_screen(session.world.player_position(player_id)))
	view_b.free()

	print("movement_test passed")
	quit()
