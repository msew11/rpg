## 在活跃区块上绘制 monster 的临时可见标记。
class_name MonsterLayer
extends Node2D

## 视图读取的世界状态。
var source: WorldState


## 绑定世界并在区块或 monster 变化时重绘。
func bind(world: WorldState) -> void:
	if source != null:
		source.chunks_changed.disconnect(_on_chunks_changed)
	source = world
	source.chunks_changed.connect(_on_chunks_changed)
	queue_redraw()


## 在世界内容变化后刷新标记。
func _on_chunks_changed() -> void:
	queue_redraw()


## 返回当前 monster 标记的等轴画面位置，供视图验收读取。
func visible_positions() -> PackedVector2Array:
	var result := PackedVector2Array()
	if source == null:
		return result
	for world_position: Vector2 in source.visible_monster_positions():
		result.append(UtilIsometricProjection.to_screen(world_position))
	return result


## 以醒目的临时图形绘制每个活跃 monster。
func _draw() -> void:
	for screen_position: Vector2 in visible_positions():
		draw_circle(screen_position + Vector2(0, -3), 13.0, Color(0.09, 0.03, 0.12, 0.55))
		var head := screen_position + Vector2(0, -20)
		draw_circle(head, 15.0, Color("3c103c"))
		draw_circle(head, 12.0, Color("fb50bc"))
		draw_colored_polygon(PackedVector2Array([
			head + Vector2(-10, -7), head + Vector2(-9, -24), head + Vector2(-2, -11)
		]), Color("fb50bc"))
		draw_colored_polygon(PackedVector2Array([
			head + Vector2(2, -11), head + Vector2(9, -24), head + Vector2(10, -7)
		]), Color("fb50bc"))
		draw_circle(head + Vector2(-5, -1), 2.5, Color.WHITE)
		draw_circle(head + Vector2(5, -1), 2.5, Color.WHITE)
