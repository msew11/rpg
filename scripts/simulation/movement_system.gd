class_name MovementSystem
extends RefCounted


static func step(world: WorldState, delta: float) -> void:
	for entity_id in world.positions:
		var screen_direction: Vector2i = world.move_intents[entity_id]
		if screen_direction == Vector2i.ZERO:
			continue
		var world_direction := IsometricProjection.to_world(Vector2(screen_direction)).normalized()
		world.positions[entity_id] += world_direction * world.speeds[entity_id] * delta
		world.facings[entity_id] = screen_direction
