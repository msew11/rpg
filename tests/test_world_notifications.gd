## 验证 openspec/specs/world-interaction-events 的真实变化通知场景。
class_name TestWorldNotifications
extends SceneTree


## 检查无效、重复及有效交互的玩家、区块和调试通知。
func _initialize() -> void:
	var cache := LocalDataRepoCache.new()
	var world: WorldManager = WorldManager.Create(cache)
	var player_id: int = world.initialize_session(Vector2(6, 6), 3.0, Vector2i(0, 1), ManualChunkCfg.new())
	var player_changes: Array[int] = []
	var chunk_changes: Array[int] = []
	var debug_changes: Array[int] = []
	var on_player_changed := func(id: int) -> void: player_changes.append(id)
	var on_chunks_changed := func() -> void: chunk_changes.append(1)
	var on_debug_changed := func() -> void: debug_changes.append(1)
	world.player_changed.connect(on_player_changed)
	world.chunks_changed.connect(on_chunks_changed)
	world.debug_changed.connect(on_debug_changed)

	assert(world.send_event(SetDebugEdgesEvent.Create(false)))
	assert(debug_changes.is_empty())
	assert(world.send_event(SetDebugEdgesEvent.Create(true)))
	assert(debug_changes.size() == 1)
	assert(world.send_event(SetDebugEdgesEvent.Create(true)))
	assert(debug_changes.size() == 1)

	assert(world.send_event(ToggleTileEvent.Create(Vector2(25, 25))))
	assert(chunk_changes.is_empty())
	assert(world.send_event(ToggleTileEvent.Create(Vector2.ZERO)))
	assert(chunk_changes.size() == 1)
	assert(world.send_event(SpawnDebugMonsterEvent.Create(Vector2(25, 25))))
	assert(chunk_changes.size() == 1)
	var spawn := SpawnDebugMonsterEvent.Create(Vector2.ZERO)
	assert(world.send_event(spawn) and spawn.entity_id != 0)
	assert(chunk_changes.size() == 2)

	assert(world.send_event(MoveIntentEvent.Create(-1, Vector2i.RIGHT)))
	world.step(0.0)
	assert(player_changes.is_empty() and chunk_changes.size() == 2)
	assert(world.send_event(MoveIntentEvent.Create(player_id, Vector2i.RIGHT)))
	assert(player_changes.is_empty())
	world.step(0.0)
	assert(player_changes == [player_id])
	world.step(0.0)
	assert(player_changes.size() == 1)
	world.step(1.0)
	assert(player_changes.size() == 2)
	assert(chunk_changes.size() == 2)

	world.player_changed.disconnect(on_player_changed)
	world.chunks_changed.disconnect(on_chunks_changed)
	world.debug_changed.disconnect(on_debug_changed)
	print("test_world_notifications passed")
	quit()
