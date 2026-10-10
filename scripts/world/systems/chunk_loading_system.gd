## 根据本地玩家位置更新世界实体的附近行走区块。
class_name ChunkLoadingSystem
extends EcsSystem

const LOAD_RADIUS := 1


## 在移动系统之后计算有限的区块显示范围。
func update(world: EcsWorld, _delta: float) -> void:
	var world_ids := world.query([WorldChunksComponent])
	var player_ids := world.query([PositionComponent, LocalPlayerComponent])
	if world_ids.is_empty() or player_ids.is_empty():
		return
	var chunks := world.get_component(world_ids[0], WorldChunksComponent) as WorldChunksComponent
	if chunks.chunk_size == Vector2i.ZERO:
		return
	var position := world.get_component(player_ids[0], PositionComponent) as PositionComponent
	var center := UtilChunkCoordinates.raw_chunk(UtilChunkCoordinates.tile_at(position.value), chunks.chunk_size)
	var loaded: Array[Vector2i] = []
	for y in range(center.y - LOAD_RADIUS, center.y + LOAD_RADIUS + 1):
		for x in range(center.x - LOAD_RADIUS, center.x + LOAD_RADIUS + 1):
			loaded.append(Vector2i(x, y))
	if chunks.loaded != loaded:
		chunks.loaded = loaded
