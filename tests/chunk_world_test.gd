## 验证区块加载、回环和运行期地块状态。
extends SceneTree


## 运行区块世界行为断言并退出。
func _initialize() -> void:
	var source := WorldState.new()
	var catalog := TestChunkCatalog.new()
	source.initialize_test_chunks(catalog)
	var player_id := source.create_player(Vector2(1, 1), 3.0)
	source.step(0.0)
	var initial_loaded := source.chunks().loaded
	assert(initial_loaded.size() == 9)
	assert(initial_loaded.has(Vector2i(0, 0)))
	var before := source.tile_type(Vector2i(1, 1))
	source.toggle_tile(Vector2(1, 1))
	assert(source.tile_type(Vector2i(1, 1)) == 1 - before)
	source.set_player_position(player_id, Vector2(25, 25))
	source.step(0.0)
	var distant_loaded := source.chunks().loaded
	assert(distant_loaded.size() == 9)
	assert(not distant_loaded.has(Vector2i(0, 0)))
	source.set_player_position(player_id, Vector2(1, 1))
	source.step(0.0)
	assert(source.tile_type(Vector2i(1, 1)) == 1 - before)
	assert(source.tile_type(Vector2i(49, 1)) == 1 - before)
	assert(source.tile_type(Vector2i(1, -47)) == 1 - before)
	source.set_player_position(player_id, Vector2(11.9, 1.0))
	source.step(0.0)
	source.set_player_intent(player_id, Vector2i.RIGHT)
	source.step(1.0)
	assert(source.chunks().loaded.has(Vector2i(2, 0)))
	assert(not source.chunks().loaded.has(Vector2i(-1, 0)))
	print("chunk_world_test passed")
	quit()
