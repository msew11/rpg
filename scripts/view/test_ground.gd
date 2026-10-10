extends Node2D

const GRASS := preload("res://assets/kenney_isometric_roads/grass.png")
const DIRT := preload("res://assets/kenney_isometric_roads/dirt.png")
const FIELD_RADIUS := 30


func _draw() -> void:
	# Draw far tiles first so the small raised edges overlap correctly.
	for row in range(-FIELD_RADIUS * 2, FIELD_RADIUS * 2 + 1):
		for x in range(-FIELD_RADIUS, FIELD_RADIUS + 1):
			var y := row - x
			if abs(y) > FIELD_RADIUS:
				continue
			var tile_position := IsometricProjection.to_screen(Vector2(x, y))
			var tile: Texture2D = DIRT if (x == 0 or y == 0) else GRASS
			draw_texture(tile, tile_position - Vector2(50, 25))
