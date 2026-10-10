class_name WorldState
extends RefCounted

# Component tables are keyed by stable entity ID and contain no scene nodes.
var positions: Dictionary = {}
var move_intents: Dictionary = {}
var speeds: Dictionary = {}
var facings: Dictionary = {}
var _next_entity_id := 1


func create_entity(position: Vector2, speed: float) -> int:
	var entity_id := _next_entity_id
	_next_entity_id += 1
	positions[entity_id] = position
	move_intents[entity_id] = Vector2.ZERO
	speeds[entity_id] = speed
	facings[entity_id] = Vector2i(0, 1)
	return entity_id
