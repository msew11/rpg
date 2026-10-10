## 根据 ECS 世界和玩家状态计算完整地图的坐标与区块状态。
class_name WorldMapSystem
extends EcsSystem

const CHUNK_AXIS_PIXELS := 58.0

var _source_ref: WeakRef
var player_id: int
var map_size := Vector2.ZERO
var pan_offset := Vector2.ZERO
var _loaded_ids: Array[Vector2i] = []


## 取得仍然存活的世界封装，避免 ECS 系统与世界互相持有。
func _source() -> WorldState:
	return _source_ref.get_ref() as WorldState


## 绑定要展示的世界与玩家实体。
func _init(world: WorldState, entity_id: int) -> void:
	_source_ref = weakref(world)
	player_id = entity_id
	refresh()


## 在 ECS 时间步后更新已加载区块的规范 ID。
func update(_world: EcsWorld, _delta: float) -> void:
	refresh()


## 根据行走区块范围更新已加载的规范区块集合。
func refresh() -> void:
	_loaded_ids.clear()
	var chunks := _source().chunks()
	if chunks.world_size == Vector2i.ZERO:
		return
	for raw in chunks.loaded:
		var canonical := ChunkCoordinates.canonical_chunk(raw, chunks.world_size)
		if not _loaded_ids.has(canonical):
			_loaded_ids.append(canonical)


## 将世界位置映射到可平移的地图坐标。
func map_point(world_position: Vector2) -> Vector2:
	var chunks := _source().chunks()
	var relative := world_position / Vector2(chunks.chunk_size) - Vector2(chunks.world_size) / 2.0
	return map_size / 2.0 + pan_offset + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


## 计算规范区块在地图上的四个顶点。
func chunk_corners(canonical: Vector2i) -> PackedVector2Array:
	var chunk_size := _source().chunks().chunk_size
	var origin := Vector2(ChunkCoordinates.tile_origin(canonical, chunk_size))
	return PackedVector2Array([
		map_point(origin),
		map_point(origin + Vector2(chunk_size.x, 0)),
		map_point(origin + Vector2(chunk_size)),
		map_point(origin + Vector2(0, chunk_size.y))
	])


## 将玩家位置回环到有限地图后定位标记。
func player_marker_position() -> Vector2:
	var chunks := _source().chunks()
	var period := Vector2(chunks.world_size * chunks.chunk_size)
	var position := _source().player_position(player_id)
	return map_point(Vector2(fposmod(position.x, period.x), fposmod(position.y, period.y)))


## 返回玩家所在的规范区块。
func current_chunk() -> Vector2i:
	var raw := ChunkCoordinates.raw_chunk(ChunkCoordinates.tile_at(_source().player_position(player_id)), _source().chunks().chunk_size)
	return ChunkCoordinates.canonical_chunk(raw, _source().chunks().world_size)


## 判断规范区块是否属于当前加载范围。
func is_loaded(canonical: Vector2i) -> bool:
	return _loaded_ids.has(canonical)
