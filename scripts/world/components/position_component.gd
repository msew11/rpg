## 保存实体在逻辑世界中的连续位置。
class_name PositionComponent
extends EcsComponent

## 实体在逻辑世界中的连续位置。
var value: Vector2


## 设置初始逻辑位置。
func _init(initial: Vector2 = Vector2.ZERO) -> void:
	value = initial
