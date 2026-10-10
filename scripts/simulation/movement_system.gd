class_name MovementSystem
extends EcsSystem


func update(world: EcsWorld, delta: float) -> void:
	for entity_id in world.query([PositionComponent, MovementIntentComponent, MoveSpeedComponent, FacingComponent]):
		var intent := world.get_component(entity_id, MovementIntentComponent) as MovementIntentComponent
		if intent.value == Vector2i.ZERO:
			continue
		var position := world.get_component(entity_id, PositionComponent) as PositionComponent
		var speed := world.get_component(entity_id, MoveSpeedComponent) as MoveSpeedComponent
		var facing := world.get_component(entity_id, FacingComponent) as FacingComponent
		position.value += IsometricProjection.movement_direction(intent.value) * speed.value * delta
		facing.value = intent.value
