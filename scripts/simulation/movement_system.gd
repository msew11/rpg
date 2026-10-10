## 根据移动意图在 ECS 固定步中更新位置与朝向。
class_name MovementSystem
extends EcsSystem


## 移动有输入的实体，并保留静止实体的朝向。
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
