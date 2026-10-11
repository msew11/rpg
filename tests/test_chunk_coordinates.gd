## 验证 openspec/specs/continuous-chunk-world/spec.md 的负坐标与回环 ID 场景。
class_name TestChunkCoordinates
extends SceneTree


## 运行区块坐标边界断言并退出。
func _initialize() -> void:
	var chunk_size := Vector2i(12, 12)
	var world_size := Vector2i(4, 4)
	assert(UtilChunkCoordinates.tile_at(Vector2(-0.1, -12.1)) == Vector2i(-1, -13))
	assert(UtilChunkCoordinates.raw_chunk(Vector2i(-1, -13), chunk_size) == Vector2i(-1, -2))
	assert(UtilChunkCoordinates.local_tile(Vector2i(-1, -13), chunk_size) == Vector2i(11, 11))
	assert(UtilChunkCoordinates.canonical_chunk(Vector2i(-1, -2), world_size) == Vector2i(3, 2))
	for x in range(-60, 61):
		for y in range(-60, 61):
			var tile := Vector2i(x, y)
			var raw := UtilChunkCoordinates.raw_chunk(tile, chunk_size)
			var local := UtilChunkCoordinates.local_tile(tile, chunk_size)
			assert(UtilChunkCoordinates.tile_origin(raw, chunk_size) + local == tile)
			assert(UtilChunkCoordinates.canonical_chunk(raw, world_size) == UtilChunkCoordinates.canonical_chunk(raw + world_size, world_size))
	print("test_chunk_coordinates passed")
	quit()
