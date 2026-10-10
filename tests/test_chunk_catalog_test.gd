## 验证测试区块目录的尺寸和图案读取。
extends SceneTree


## 检查目录布局与样例地块并退出。
func _initialize() -> void:
	var catalog := TestChunkCatalog.new()
	assert(catalog.chunk_size == Vector2i(12, 12))
	assert(catalog.world_size == Vector2i(4, 4))
	for y in range(catalog.world_size.y):
		for x in range(catalog.world_size.x):
			assert(catalog.tiles_for(Vector2i(x, y)).size() == 144)
	var meadow := catalog.tiles_for(Vector2i(0, 0))
	var paths := catalog.tiles_for(Vector2i(1, 0))
	assert(meadow != paths)
	assert(meadow[0] == 0)
	assert(paths[5] == 1)
	print("test_chunk_catalog_test passed")
	quit()
