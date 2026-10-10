class_name IsometricProjection
extends RefCounted

const TILE_HALF_WIDTH := 50.0
const TILE_HALF_HEIGHT := 25.0


static func to_screen(world_position: Vector2) -> Vector2:
	return Vector2(
		(world_position.x - world_position.y) * TILE_HALF_WIDTH,
		(world_position.x + world_position.y) * TILE_HALF_HEIGHT
	)


static func to_world(screen_position: Vector2) -> Vector2:
	return Vector2(
		0.5 * (screen_position.x / TILE_HALF_WIDTH + screen_position.y / TILE_HALF_HEIGHT),
		0.5 * (screen_position.y / TILE_HALF_HEIGHT - screen_position.x / TILE_HALF_WIDTH)
	)
