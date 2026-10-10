## 验证区块加载、回环和运行期地块状态。
extends SceneTree


## 运行区块世界行为断言并退出。
func _initialize() -> void:
	var source := WorldState.new()
	var catalog := TestChunkCatalog.new()
	var chunks := ChunkWorld.new(source, catalog)
	chunks.load_around(Vector2(1, 1))
	var initial_loaded := source.chunks().loaded
	assert(initial_loaded.size() == 9)
	assert(initial_loaded.has(Vector2i(0, 0)))
	var before := chunks.tile_type(Vector2i(1, 1))
	chunks.toggle_tile(Vector2(1, 1))
	assert(chunks.tile_type(Vector2i(1, 1)) == 1 - before)
	chunks.load_around(Vector2(25, 25))
	var distant_loaded := source.chunks().loaded
	assert(distant_loaded.size() == 9)
	assert(not distant_loaded.has(Vector2i(0, 0)))
	chunks.load_around(Vector2(1, 1))
	assert(chunks.tile_type(Vector2i(1, 1)) == 1 - before)
	assert(chunks.tile_type(Vector2i(49, 1)) == 1 - before)
	assert(chunks.tile_type(Vector2i(1, -47)) == 1 - before)
	print("chunk_world_test passed")
	quit()
