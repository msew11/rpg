## 汇总方向键状态并生成屏幕移动输入。
class_name MovementInput
extends RefCounted


## 合并四个方向状态，相反输入相互抵消。
static func from_axes(left: bool, right: bool, up: bool, down: bool) -> Vector2i:
	return Vector2i(int(right) - int(left), int(down) - int(up))


## 读取 WASD 和方向键的当前按下状态。
static func from_keyboard() -> Vector2i:
	return from_axes(
		Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT),
		Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT),
		Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP),
		Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN)
	)
