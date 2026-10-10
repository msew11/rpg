class_name MovementInput
extends RefCounted


static func from_axes(left: bool, right: bool, up: bool, down: bool) -> Vector2i:
	return Vector2i(int(right) - int(left), int(down) - int(up))


static func from_keyboard() -> Vector2i:
	return from_axes(
		Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT),
		Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT),
		Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP),
		Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN)
	)
