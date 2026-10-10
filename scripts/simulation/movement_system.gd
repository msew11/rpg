class_name MovementSystem
extends RefCounted


static func step(world: WorldState, delta: float) -> void:
	for entity_id in world.entity_ids:
		var screen_direction: Vector2i = world.read_value(WorldState.intent_key(entity_id))
		if screen_direction == Vector2i.ZERO:
			continue
		var world_direction := IsometricProjection.to_world(Vector2(screen_direction)).normalized()
		var position: Vector2 = world.read_value(WorldState.position_key(entity_id))
		var speed: float = world.read_value(WorldState.speed_key(entity_id))
		world.write_value(WorldState.position_key(entity_id), position + world_direction * speed * delta)
		world.write_value(WorldState.facing_key(entity_id), screen_direction)
