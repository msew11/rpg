## 验证 openspec/specs/continuous-chunk-world/spec.md 的手工测试区块内容场景。
class_name TestManualChunkCfg
extends SceneTree


## 检查目录布局与样例地块并退出。
func _initialize() -> void:
	var cfg := ManualChunkCfg.new()
	assert(cfg.chunk_size == Vector2i(12, 12))
	assert(cfg.world_size == Vector2i(4, 4))
	for y in range(cfg.world_size.y):
		for x in range(cfg.world_size.x):
			assert(cfg.tiles_for(Vector2i(x, y)).size() == 144)
	var meadow := cfg.tiles_for(Vector2i(0, 0))
	var paths := cfg.tiles_for(Vector2i(1, 0))
	assert(meadow != paths)
	assert(meadow[0] == 0)
	assert(paths[5] == 1)
	print("test_manual_chunk_cfg passed")
	quit()
