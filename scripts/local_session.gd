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
