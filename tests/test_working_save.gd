## 验证 openspec/specs/session-working-save、entity-persistence、world-script-boundaries 与 system-chat 的卸载、实体恢复和错误提示场景。
class_name TestWorkingSave
extends SceneTree

## 用于验收会话和 Chat 的主场景。
const MAIN := preload("res://scenes/main.tscn")


## 延后执行需要挂入场景树的验收场景。
func _initialize() -> void:
	call_deferred("run_test")


## 验证写入失败仍卸载，以及成功记录与活跃数据一同进入正式槽位。
func run_test() -> void:
	var directory := "user://tests/working_save_%d" % Time.get_ticks_usec()
	var slots := SaveSlots.new(directory)
	var main := MAIN.instantiate()
	main.save_slots = slots
	root.add_child(main)
	main.set_physics_process(false)
	main.start_new_game()
	var ecs: EcsWorld = WorldEcsTestAccess.FromCache(main.session.cache)
	var initial: int = main.session.world_manager.tile_type(Vector2i.ZERO)
	main.session.world_manager.send_event(ToggleTileEvent.Create(Vector2.ZERO))
	var chunk_id: int = main.session.world_manager.chunk_entity(Vector2i.ZERO)
	var child_id: int = ecs.CreateEntity(chunk_id)
	var child_position := PositionComponent.new()
	child_position.value = Vector2(4, 5)
	ecs.AddComponent(child_id, child_position)
	main.session.working_store.fail_writes = true
	main.session.world_manager.set_player_position(main.session.player_id, Vector2(25, 25))
	main.session.world_manager.step(0.0)
	assert(not main.session.world_manager.chunks().has_chunk(Vector2i.ZERO))
	assert(main.session.world_manager.chunks().active_count() <= 9)
	assert(main.chat_panel.history.get_parsed_text().contains("临时存档写入失败"))
	main.chat_panel.add_system_message("第二条测试消息")
	var messages: String = main.chat_panel.history.get_parsed_text()
	assert(messages.find("临时存档写入失败") < messages.find("第二条测试消息"))
	main.session.working_store.fail_writes = false
	main.session.world_manager.set_player_position(main.session.player_id, Vector2.ZERO)
	main.session.world_manager.step(0.0)
	assert(main.session.world_manager.tile_type(Vector2i.ZERO) == initial)
	assert(ecs.GetChildren(main.session.world_manager.chunk_entity(Vector2i.ZERO)).is_empty())

	main.session.world_manager.send_event(ToggleTileEvent.Create(Vector2.ZERO))
	chunk_id = main.session.world_manager.chunk_entity(Vector2i.ZERO)
	child_id = ecs.CreateEntity(chunk_id)
	child_position = PositionComponent.new()
	child_position.value = Vector2(7, 8)
	ecs.AddComponent(child_id, child_position)
	var destroyed_child: int = ecs.CreateEntity(chunk_id)
	ecs.AddComponent(destroyed_child, PositionComponent.new())
	assert(ecs.DestroyEntity(destroyed_child))
	main.session.world_manager.set_player_position(main.session.player_id, Vector2(25, 25))
	main.session.world_manager.step(0.0)
	assert(not main.session.world_manager.chunks().has_chunk(Vector2i.ZERO))
	main.session.world_manager.send_event(ToggleTileEvent.Create(Vector2(25, 25)))
	var distant_tile: int = main.session.world_manager.tile_type(Vector2i(25, 25))
	var active_chunk_id: int = main.session.world_manager.chunk_entity(Vector2i(2, 2))
	var active_child_id: int = ecs.CreateEntity(active_chunk_id)
	var active_position := PositionComponent.new()
	active_position.value = Vector2(26, 27)
	ecs.AddComponent(active_child_id, active_position)
	assert(slots.save(1, main.session))
	var restored := LocalSession.new(slots.read(1))
	var restored_ecs: EcsWorld = WorldEcsTestAccess.FromCache(restored.cache)
	assert(restored.player_position() == Vector2(25, 25))
	assert(restored.world_manager.tile_type(Vector2i(25, 25)) == distant_tile)
	var active_children: PackedInt64Array = restored_ecs.GetChildren(restored.world_manager.chunk_entity(Vector2i(2, 2)))
	assert(active_children.size() == 1)
	assert(restored.world_manager.player_position(active_children[0]) == Vector2(26, 27))
	restored.world_manager.set_player_position(restored.player_id, Vector2.ZERO)
	restored.world_manager.step(0.0)
	assert(restored.world_manager.tile_type(Vector2i.ZERO) == 1 - initial)
	var restored_children: PackedInt64Array = restored_ecs.GetChildren(restored.world_manager.chunk_entity(Vector2i.ZERO))
	assert(restored_children.size() == 1)
	assert(restored.world_manager.player_position(restored_children[0]) == Vector2(7, 8))
	var invalid := slots.read(1)
	invalid["changed_chunks"][0]["entities"] = [{"Components": [{"Type": "Missing.Component", "Data": "x"}], "Children": []}]
	var invalid_file := FileAccess.open("%s/slot_2.json" % directory, FileAccess.WRITE)
	invalid_file.store_string(JSON.stringify(invalid))
	invalid_file.close()
	assert(slots.inspect(2)["state"] == "invalid")

	main.start_new_game()
	assert(main.chat_panel.history.get_parsed_text().is_empty())
	main.free()
	DirAccess.remove_absolute(ProjectSettings.globalize_path("%s/slot_1.json" % directory))
	DirAccess.remove_absolute(ProjectSettings.globalize_path("%s/slot_2.json" % directory))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(directory))
	print("test_working_save passed")
	quit()
