extends Node2D

var facing := Vector2i(0, 1):
	set(value):
		facing = value
		queue_redraw()


func _draw() -> void:
	# Foot anchor is (0, 0); this placeholder makes all eight facings legible.
	draw_ellipse_shadow()
	draw_rect(Rect2(-9, -29, 18, 25), Color("26374f"))
	draw_circle(Vector2(0, -30), 10, Color("eac091"))
	var direction := facing_screen_direction()
	var arrow_start := Vector2(0, -19)
	var arrow_end := arrow_start + direction * 23.0
	draw_line(arrow_start, arrow_end, Color("fff3a6"), 4.0, true)
	var side := direction.orthogonal() * 5.0
	draw_colored_polygon(PackedVector2Array([
		arrow_end + direction * 6.0,
		arrow_end - direction * 4.0 + side,
		arrow_end - direction * 4.0 - side
	]), Color("fff3a6"))


func facing_screen_direction() -> Vector2:
	return IsometricProjection.to_screen(IsometricProjection.movement_direction(facing)).normalized()


func draw_ellipse_shadow() -> void:
	draw_set_transform(Vector2.ZERO, 0.0, Vector2(1.0, 0.38))
	draw_circle(Vector2(0, 0), 14, Color(0.08, 0.12, 0.11, 0.45))
	draw_set_transform(Vector2.ZERO)
