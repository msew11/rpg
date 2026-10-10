## 根据 ECS 世界和玩家状态计算雷达区块几何与标记。
class_name RadarSystem
extends EcsSystem

const GRID_VIEW := Rect2(16, 38, 272, 272)
const RADAR_CENTER := Vector2(152, 174)
const CHUNK_AXIS_PIXELS := 26.0

var _source_ref: WeakRef
var player_id: int
var _visible_raw: Array[Vector2i] = []


## 取得仍然存活的世界封装，避免 ECS 系统与世界互相持有。
func _source() -> WorldState:
	return _source_ref.get_ref() as WorldState


## 绑定要展示的世界与玩家实体。
func _init(world: WorldState, entity_id: int) -> void:
	_source_ref = weakref(world)
	player_id = entity_id
	refresh()


## 在 ECS 时间步后更新雷达可见范围。
func update(_world: EcsWorld, _delta: float) -> void:
	refresh()


## 根据当前玩家位置重新计算雷达视窗覆盖的区块。
func refresh() -> void:
	_visible_raw.clear()
	var chunks := _source().chunks()
	if chunks.chunk_size == Vector2i.ZERO or chunks.loaded.is_empty():
		return
	var position := _source().player_position(player_id) / Vector2(chunks.chunk_size)
	var min_chunk := Vector2(INF, INF)
	var max_chunk := Vector2(-INF, -INF)
	for screen_point in [GRID_VIEW.position, Vector2(GRID_VIEW.end.x, GRID_VIEW.position.y), GRID_VIEW.end, Vector2(GRID_VIEW.position.x, GRID_VIEW.end.y)]:
		var offset: Vector2 = (screen_point - RADAR_CENTER) / CHUNK_AXIS_PIXELS
		var chunk_point := position + Vector2((offset.x + offset.y) * 0.5, (offset.y - offset.x) * 0.5)
		min_chunk = min_chunk.min(chunk_point)
		max_chunk = max_chunk.max(chunk_point)
	for y in range(floori(min_chunk.y) - 1, ceili(max_chunk.y) + 1):
		for x in range(floori(min_chunk.x) - 1, ceili(max_chunk.x) + 1):
			var raw := Vector2i(x, y)
			var corners := chunk_corners(raw)
			var bounds := Rect2(corners[0], Vector2.ZERO)
			for corner in corners:
				bounds = bounds.expand(corner)
			if bounds.intersects(GRID_VIEW):
				_visible_raw.append(raw)


## 返回与雷达视窗相交的行走区块。
func visible_chunks() -> Array[Vector2i]:
	return _visible_raw.duplicate()


## 将世界位置映射为相对玩家的雷达坐标。
func radar_point(world_position: Vector2) -> Vector2:
	var relative := (world_position - _source().player_position(player_id)) / Vector2(_source().chunks().chunk_size)
	return RADAR_CENTER + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


## 计算行走区块在雷达上的四个顶点。
func chunk_corners(raw: Vector2i) -> PackedVector2Array:
	var chunk_size := _source().chunks().chunk_size
	var origin := Vector2(ChunkCoordinates.tile_origin(raw, chunk_size))
	return PackedVector2Array([
		radar_point(origin),
		radar_point(origin + Vector2(chunk_size.x, 0)),
		radar_point(origin + Vector2(chunk_size)),
		radar_point(origin + Vector2(0, chunk_size.y))
	])


## 返回玩家所在的行走区块。
func current_raw_chunk() -> Vector2i:
	return ChunkCoordinates.raw_chunk(ChunkCoordinates.tile_at(_source().player_position(player_id)), _source().chunks().chunk_size)


## 返回玩家所在的规范区块。
func current_canonical_chunk() -> Vector2i:
	return ChunkCoordinates.canonical_chunk(current_raw_chunk(), _source().chunks().world_size)


## 按当前、已加载或未加载状态选择轮廓类别。
func chunk_state(raw: Vector2i) -> int:
	if raw == current_raw_chunk():
		return 2
	return 1 if _source().chunks().loaded.has(raw) else 0


## 判断区块东侧是否为世界回环接缝。
func wrap_east(raw: Vector2i) -> bool:
	return posmod(raw.x + 1, _source().chunks().world_size.x) == 0


## 判断区块南侧是否为世界回环接缝。
func wrap_south(raw: Vector2i) -> bool:
	return posmod(raw.y + 1, _source().chunks().world_size.y) == 0


## 返回行走区块对应的规范坐标标签。
func chunk_label(raw: Vector2i) -> String:
	var canonical := ChunkCoordinates.canonical_chunk(raw, _source().chunks().world_size)
	return "%d,%d" % [canonical.x, canonical.y]


## 返回固定在雷达中心的玩家标记位置。
func player_marker_position() -> Vector2:
	return RADAR_CENTER


## 读取玩家朝向并转为雷达方向向量。
func facing_vector() -> Vector2:
	return Vector2(_source().player_facing(player_id)).normalized()


## 避开玩家标记安排当前区块坐标标签。
func current_label_position() -> Vector2:
	var corners := chunk_corners(current_raw_chunk())
	var center := (corners[0] + corners[2]) * 0.5
	var below := RADAR_CENTER.y < center.y or (is_equal_approx(RADAR_CENTER.y, center.y) and facing_vector().y < 0)
	return center + Vector2(-12, 14 if below else -4)
