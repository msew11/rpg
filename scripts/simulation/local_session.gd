class_name LocalSession
extends RefCounted

const PLAYER_SPEED := 3.0

var world := WorldState.new()
var player_id: int


func _init() -> void:
	player_id = world.create_entity(Vector2.ZERO, PLAYER_SPEED)


func set_player_direction(screen_direction: Vector2i) -> void:
	world.write_value(WorldState.intent_key(player_id), screen_direction)


func step(delta: float) -> void:
	MovementSystem.step(world, delta)


func player_position() -> Vector2:
	return world.read_value(WorldState.position_key(player_id))


func player_facing() -> Vector2i:
	return world.read_value(WorldState.facing_key(player_id))
