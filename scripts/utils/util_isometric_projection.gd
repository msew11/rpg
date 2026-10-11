## 转换逻辑世界坐标与等轴屏幕坐标。
class_name UtilIsometricProjection
extends RefCounted

## 等轴地块在屏幕上的半宽。
const TILE_HALF_WIDTH := 50.0
## 等轴地块在屏幕上的半高。
const TILE_HALF_HEIGHT := 25.0


## 将世界位置投影到屏幕坐标。
static func to_screen(world_position: Vector2) -> Vector2:
	return Vector2(
		(world_position.x - world_position.y) * TILE_HALF_WIDTH,
		(world_position.x + world_position.y) * TILE_HALF_HEIGHT
	)


## 将屏幕坐标反算为世界位置。
static func to_world(screen_position: Vector2) -> Vector2:
	return Vector2(
		0.5 * (screen_position.x / TILE_HALF_WIDTH + screen_position.y / TILE_HALF_HEIGHT),
		0.5 * (screen_position.y / TILE_HALF_HEIGHT - screen_position.x / TILE_HALF_WIDTH)
	)


## 把屏幕方向输入转换为等速的世界移动方向。
static func movement_direction(screen_input: Vector2i) -> Vector2:
	var tile_aligned_input := Vector2(
		screen_input.x * TILE_HALF_WIDTH,
		screen_input.y * TILE_HALF_HEIGHT
	)
	return to_world(tile_aligned_input).normalized()
