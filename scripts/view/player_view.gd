## 绘制玩家占位形象及其八方向朝向。
extends Node2D

var facing := Vector2i(0, 1):
	set(value):
		facing = value
		queue_redraw()


## 绘制以脚部为锚点的占位角色和朝向箭头。
func _draw() -> void:
	# 脚部锚点为原点，占位形象用箭头区分八个朝向。
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


## 将逻辑朝向转换为屏幕上的箭头方向。
func facing_screen_direction() -> Vector2:
	return IsometricProjection.to_screen(IsometricProjection.movement_direction(facing)).normalized()


## 在脚部绘制压扁的角色阴影。
func draw_ellipse_shadow() -> void:
	draw_set_transform(Vector2.ZERO, 0.0, Vector2(1.0, 0.38))
	draw_circle(Vector2(0, 0), 14, Color(0.08, 0.12, 0.11, 0.45))
	draw_set_transform(Vector2.ZERO)
