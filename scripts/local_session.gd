## 封装本地玩家会话，向世界提交输入并读取玩家状态。
class_name LocalSession
extends RefCounted

## 本地玩家每秒在逻辑世界中的移动距离。
const PLAYER_SPEED := 3.0

## 本地会话独占的数据仓库缓存。
var cache := LocalDataRepoCache.new()
## 本地会话持有的世界状态。
var world: WorldState
## 本地玩家的 ECS 实体标识。
var player_id: int


## 在世界中创建本地玩家实体。
func _init() -> void:
	world = WorldState.Create(cache)
	player_id = world.create_player(Vector2.ZERO, PLAYER_SPEED)
	world.initialize_test_chunks(ManualChunkCfg.new())


## 提交玩家的屏幕移动方向。
func set_player_direction(screen_direction: Vector2i) -> void:
	world.set_player_intent(player_id, screen_direction)


## 推进本地世界的固定时间步。
func step(delta: float) -> void:
	world.step(delta)


## 读取玩家的当前逻辑位置。
func player_position() -> Vector2:
	return world.player_position(player_id)


## 读取玩家保留的朝向。
func player_facing() -> Vector2i:
	return world.player_facing(player_id)


## 收集跨会话需要保留的玩家状态和已改变的测试区块。
func capture_snapshot() -> Dictionary:
	var position := player_position()
	var facing := player_facing()
	var config := ManualChunkCfg.new()
	var changed_chunks: Array[Dictionary] = []
	for y in range(config.world_size.y):
		for x in range(config.world_size.x):
			var id := Vector2i(x, y)
			var tiles: PackedByteArray = world.chunks().tiles_for(id)
			if tiles != config.tiles_for(id):
				changed_chunks.append({"id": [x, y], "tiles": Array(tiles)})
	return {
		"position": [position.x, position.y],
		"facing": [facing.x, facing.y],
		"chunk_size": [config.chunk_size.x, config.chunk_size.y],
		"world_size": [config.world_size.x, config.world_size.y],
		"changed_chunks": changed_chunks,
	}


## 将已验证的存档状态恢复到当前新建的本地会话。
func restore_snapshot(snapshot: Dictionary) -> void:
	for entry: Dictionary in snapshot["changed_chunks"]:
		var id: Array = entry["id"]
		world.set_chunk_tiles(Vector2i(int(id[0]), int(id[1])), PackedByteArray(entry["tiles"]))
	var position: Array = snapshot["position"]
	var facing: Array = snapshot["facing"]
	world.set_player_position(player_id, Vector2(float(position[0]), float(position[1])))
	world.set_player_facing(player_id, Vector2i(int(facing[0]), int(facing[1])))
	world.step(0.0)
