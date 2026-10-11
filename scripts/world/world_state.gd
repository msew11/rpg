## 管理世界实体和玩家实体，并向展示层发出明确的状态变化通知。
class_name WorldState
extends RefCounted

signal player_changed(entity_id: int)
signal chunks_changed
signal debug_changed

## 保存实体组件并调度系统的 ECS 世界。
var ecs := EcsWorld.new()
## 持有世界区块和调试组件的实体标识。
var world_entity_id: int


## 创建世界实体及其组件，并按顺序注册移动与区块加载系统。
func _init() -> void:
	world_entity_id = ecs.create_entity()
	ecs.add_component(world_entity_id, WorldChunksComponent.new())
	ecs.add_component(world_entity_id, WorldDebugComponent.new())
	ecs.add_system(MovementSystem.new())
	ecs.add_system(ChunkLoadingSystem.new())


## 创建具有位置、意图、速度与朝向的玩家实体。
func create_player(position: Vector2, speed: float) -> int:
	var entity_id := ecs.create_entity()
	ecs.add_component(entity_id, PositionComponent.new(position))
	ecs.add_component(entity_id, MovementIntentComponent.new())
	ecs.add_component(entity_id, MoveSpeedComponent.new(speed))
	ecs.add_component(entity_id, FacingComponent.new())
	ecs.add_component(entity_id, LocalPlayerComponent.new())
	player_changed.emit(entity_id)
	return entity_id


## 推进 ECS，并在玩家位置或朝向变化时通知视图。
func step(delta: float) -> void:
	var previous: Dictionary = {}
	var previous_loaded := chunks().loaded.duplicate()
	for entity_id in ecs.query([PositionComponent, FacingComponent]):
		previous[entity_id] = [player_position(entity_id), player_facing(entity_id)]
	ecs.step(delta)
	if chunks().loaded != previous_loaded:
		chunks_changed.emit()
	for entity_id in previous:
		if player_position(entity_id) != previous[entity_id][0] or player_facing(entity_id) != previous[entity_id][1]:
			player_changed.emit(entity_id)


## 读取世界实体的区块组件。
func chunks() -> WorldChunksComponent:
	return ecs.get_component(world_entity_id, WorldChunksComponent) as WorldChunksComponent


## 读取世界实体的调试显示组件。
func debug() -> WorldDebugComponent:
	return ecs.get_component(world_entity_id, WorldDebugComponent) as WorldDebugComponent


## 读取玩家的连续世界位置。
func player_position(entity_id: int) -> Vector2:
	var position := ecs.get_component(entity_id, PositionComponent) as PositionComponent
	return position.value


## 读取玩家当前保留的朝向。
func player_facing(entity_id: int) -> Vector2i:
	var facing := ecs.get_component(entity_id, FacingComponent) as FacingComponent
	return facing.value


## 设置玩家移动意图，供下一次 ECS 更新使用。
func set_player_intent(entity_id: int, direction: Vector2i) -> void:
	var intent := ecs.get_component(entity_id, MovementIntentComponent) as MovementIntentComponent
	intent.value = direction


## 设置玩家位置，并在有效变化时通知视图。
func set_player_position(entity_id: int, value: Vector2) -> void:
	var position := ecs.get_component(entity_id, PositionComponent) as PositionComponent
	if position.value == value:
		return
	position.value = value
	player_changed.emit(entity_id)


## 读取指定地块在规范区块中的当前类型。
func tile_type(tile: Vector2i) -> int:
	var chunk_size := chunks().chunk_size
	var raw := UtilChunkCoordinates.raw_chunk(tile, chunk_size)
	var canonical := UtilChunkCoordinates.canonical_chunk(raw, chunks().world_size)
	var local := UtilChunkCoordinates.local_tile(tile, chunk_size)
	return chunks().tiles_for(canonical)[local.y * chunk_size.x + local.x]


## 切换指定世界位置的测试地块类型。
func toggle_tile(world_position: Vector2) -> void:
	var tile := UtilChunkCoordinates.tile_at(world_position)
	var chunk_size := chunks().chunk_size
	var raw := UtilChunkCoordinates.raw_chunk(tile, chunk_size)
	var canonical := UtilChunkCoordinates.canonical_chunk(raw, chunks().world_size)
	var local := UtilChunkCoordinates.local_tile(tile, chunk_size)
	var tiles := chunks().tiles_for(canonical)
	var index := local.y * chunk_size.x + local.x
	tiles[index] = 1 - tiles[index]
	set_chunk_tiles(canonical, tiles)


## 将固定测试目录一次写入世界实体的区块组件。
func initialize_test_chunks(cfg: ManualChunkCfg) -> void:
	chunks().chunk_size = cfg.chunk_size
	chunks().world_size = cfg.world_size
	for y in range(cfg.world_size.y):
		for x in range(cfg.world_size.x):
			set_chunk_tiles(Vector2i(x, y), cfg.tiles_for(Vector2i(x, y)))
	step(0.0)


## 设置玩家朝向，并在有效变化时通知视图。
func set_player_facing(entity_id: int, value: Vector2i) -> void:
	var facing := ecs.get_component(entity_id, FacingComponent) as FacingComponent
	if facing.value == value:
		return
	facing.value = value
	player_changed.emit(entity_id)


## 保存一个规范区块的地块，并通知依赖它的视图。
func set_chunk_tiles(canonical_id: Vector2i, tiles: PackedByteArray) -> bool:
	if not chunks().set_tiles(canonical_id, tiles):
		return false
	chunks_changed.emit()
	return true


## 切换区块边缘显示，并通知调试视图。
func set_debug_edges(enabled: bool) -> void:
	if debug().edges == enabled:
		return
	debug().edges = enabled
	debug_changed.emit()


## 切换雷达显示，并通知调试视图。
func set_debug_radar(enabled: bool) -> void:
	if debug().radar == enabled:
		return
	debug().radar = enabled
	debug_changed.emit()
