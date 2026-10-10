## 封装本地玩家会话，向世界提交输入并读取玩家状态。
class_name LocalSession
extends RefCounted

const PLAYER_SPEED := 3.0

var world := WorldState.new()
var player_id: int


## 在世界中创建本地玩家实体。
func _init() -> void:
	player_id = world.create_entity(Vector2.ZERO, PLAYER_SPEED)


## 提交玩家的屏幕移动方向。
func set_player_direction(screen_direction: Vector2i) -> void:
	world.write_value(WorldState.intent_key(player_id), screen_direction)


## 推进本地世界的固定时间步。
func step(delta: float) -> void:
	world.step(delta)


## 读取玩家的当前逻辑位置。
func player_position() -> Vector2:
	return world.read_value(WorldState.position_key(player_id))


## 读取玩家保留的朝向。
func player_facing() -> Vector2i:
	return world.read_value(WorldState.facing_key(player_id))
