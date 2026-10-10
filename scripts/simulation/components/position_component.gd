class_name PositionComponent
extends EcsComponent

var value: Vector2


func _init(initial: Vector2 = Vector2.ZERO) -> void:
	value = initial
