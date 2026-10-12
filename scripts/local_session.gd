## 封装本地玩家会话，向世界提交输入并读取玩家状态。
class_name LocalSession
extends RefCounted

## 本地玩家每秒在逻辑世界中的移动距离。
const PLAYER_SPEED := 3.0

## 本地会话独占的数据仓库缓存。
var cache := LocalDataRepoCache.new()
## 会话唯一的临时存档工作副本。
var working_store: WorkingSaveStore
## 本地会话持有的世界状态。
var world_manager: WorldManager
## 本地玩家的 ECS 实体标识。
var player_id: int


## 在世界中创建本地玩家实体。
func _init(initial: Dictionary = {}) -> void:
	_start(initial)


## 用给定工作快照重建独立的缓存、世界和玩家。
func _start(initial: Dictionary) -> void:
	cache = LocalDataRepoCache.new()
	working_store = WorkingSaveStore.new(initial)
	cache.bind_working_store(working_store)
	world_manager = WorldManager.Create(cache)
	var position := Vector2.ZERO
	if initial.has("position"):
		position = Vector2(float(initial["position"][0]), float(initial["position"][1]))
	var facing := Vector2i(0, 1)
	if initial.has("facing"):
		facing = Vector2i(int(initial["facing"][0]), int(initial["facing"][1]))
	player_id = world_manager.initialize_session(position, PLAYER_SPEED, facing, ManualChunkCfg.new())


## 提交玩家的屏幕移动方向并返回命令结果。
func set_player_direction(screen_direction: Vector2i) -> WorldEventResult:
	var command := MoveIntentEvent.new()
	command.entity_id = player_id
	command.direction = screen_direction
	return world_manager.send_event(command)


## 切换玩家当前所在的测试地块。
func toggle_player_tile() -> WorldEventResult:
	return world_manager.send_event(ToggleTileEvent.Create(player_position()))


## 设置区块边缘线显示状态。
func set_debug_edges(enabled: bool) -> WorldEventResult:
	return world_manager.send_event(SetDebugEdgesEvent.Create(enabled))


## 切换区块边缘线显示状态。
func toggle_debug_edges() -> WorldEventResult:
	return set_debug_edges(not world_manager.debug().edges)


## 设置雷达显示状态。
func set_debug_radar(enabled: bool) -> WorldEventResult:
	return world_manager.send_event(SetDebugRadarEvent.Create(enabled))


## 在玩家附近提交测试怪物创建命令。
func spawn_debug_monster() -> WorldEventResult:
	return world_manager.send_event(SpawnDebugMonsterEvent.Create(player_position()))


## 推进本地世界的固定时间步。
func step(delta: float) -> void:
	world_manager.step(delta)


## 读取玩家的当前逻辑位置。
func player_position() -> Vector2:
	return world_manager.player_position(player_id)


## 读取玩家保留的朝向。
func player_facing() -> Vector2i:
	return world_manager.player_facing(player_id)


## 合并临时存档与活跃区块，生成本次正式保存的状态。
func capture_snapshot() -> Dictionary:
	var position := player_position()
	var facing := player_facing()
	var config := ManualChunkCfg.new()
	var result := working_store.snapshot.duplicate(true)
	var seen := {}
	for raw: Vector2i in world_manager.chunks().loaded:
		var id := UtilChunkCoordinates.canonical_chunk(raw, config.world_size)
		if seen.has(id):
			continue
		seen[id] = true
		working_store.merge_chunk_into(result, id, world_manager.chunks().tiles_for(id), config.tiles_for(id), world_manager.capture_chunk_entities(raw))
	result["position"] = [position.x, position.y]
	result["facing"] = [facing.x, facing.y]
	result["chunk_size"] = [config.chunk_size.x, config.chunk_size.y]
	result["world_size"] = [config.world_size.x, config.world_size.y]
	return result


## 将已验证的存档状态恢复为一套新的运行时世界。
func restore_snapshot(snapshot: Dictionary) -> void:
	_start(snapshot)
