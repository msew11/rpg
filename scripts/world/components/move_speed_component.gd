## 保存实体在逻辑世界中的移动速度。
class_name MoveSpeedComponent
extends EcsComponent

## 实体每秒在逻辑世界中的移动距离。
var value: float


## 设置初始移动速度。
func _init(initial: float = 0.0) -> void:
	value = initial
