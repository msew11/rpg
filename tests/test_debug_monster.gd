## 验证 openspec/specs/debug-monster-spawning/spec.md 的创建、区块归属与卸载恢复场景。
class_name TestDebugMonster
extends SceneTree

## 用于验收 Debug 面板入口的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 延后运行需挂入场景树的测试。
func _initialize() -> void:
	call_deferred("run_test")


## 检查按钮创建的 monster 随区块卸载并恢复。
func run_test() -> void:
	var main := MAIN.instantiate()
	root.add_child(main)
	main.set_physics_process(false)
	main.start_new_game()
	var world: WorldState = main.session.world
	var chunk_id: int = world.chunk_entity(Vector2i.ZERO)
	var before: PackedInt64Array = world.ecs.GetChildren(chunk_id)
	main.debug_panel.add_monster_button.pressed.emit()
	var children: PackedInt64Array = world.ecs.GetChildren(chunk_id)
	assert(children.size() == before.size() + 1)
	var monster_id: int = children[children.size() - 1]
	assert(world.ecs.GetParent(monster_id) == chunk_id)
	var position: Vector2 = world.player_position(monster_id)
	assert(position.x >= 0.0 and position.x < 12.0)
	assert(position.y >= 0.0 and position.y < 12.0)
	assert(world.capture_chunk_entities(Vector2i.ZERO).contains("MonsterComponent"))
	world.set_player_position(main.session.player_id, Vector2(25, 25))
	world.step(0.0)
	assert(not world.ecs.HasEntity(monster_id))
	world.set_player_position(main.session.player_id, Vector2.ZERO)
	world.step(0.0)
	var restored: PackedInt64Array = world.ecs.GetChildren(world.chunk_entity(Vector2i.ZERO))
	assert(restored.size() == children.size())
	assert(world.player_position(restored[restored.size() - 1]) == position)
	assert(world.capture_chunk_entities(Vector2i.ZERO).contains("MonsterComponent"))
	main.free()
	print("test_debug_monster passed")
	quit()
