## 按当前世界组件计算雷达坐标与区块几何。
class_name UtilRadarProjection
extends RefCounted

## 雷达中绘制区块网格的可见矩形。
const GRID_VIEW := Rect2(16, 38, 272, 272)
## 雷达中固定的玩家标记中心。
const RADAR_CENTER := Vector2(152, 174)
## 雷达中一个区块轴向的屏幕像素长度。
const CHUNK_AXIS_PIXELS := 26.0


## 返回与雷达视窗相交的行走区块。
static func visible_chunks(world: WorldState, player_id: int) -> Array[Vector2i]:
	var visible: Array[Vector2i] = []
	var chunks := world.chunks()
	if chunks.chunk_size == Vector2i.ZERO or chunks.loaded.is_empty():
		return visible
	var position := world.player_position(player_id) / Vector2(chunks.chunk_size)
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
			var corners := chunk_corners(world, player_id, raw)
			var bounds := Rect2(corners[0], Vector2.ZERO)
			for corner in corners:
				bounds = bounds.expand(corner)
			if bounds.intersects(GRID_VIEW):
				visible.append(raw)
	return visible


## 将世界位置映射为相对玩家的雷达坐标。
static func radar_point(world: WorldState, player_id: int, world_position: Vector2) -> Vector2:
	var relative := (world_position - world.player_position(player_id)) / Vector2(world.chunks().chunk_size)
	return RADAR_CENTER + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


## 计算行走区块在雷达上的四个顶点。
static func chunk_corners(world: WorldState, player_id: int, raw: Vector2i) -> PackedVector2Array:
	var chunk_size: Vector2i = world.chunks().chunk_size
	var origin := Vector2(UtilChunkCoordinates.tile_origin(raw, chunk_size))
	return PackedVector2Array([
		radar_point(world, player_id, origin),
		radar_point(world, player_id, origin + Vector2(chunk_size.x, 0)),
		radar_point(world, player_id, origin + Vector2(chunk_size)),
		radar_point(world, player_id, origin + Vector2(0, chunk_size.y))
	])


## 返回玩家所在的行走区块。
static func current_raw_chunk(world: WorldState, player_id: int) -> Vector2i:
	return UtilChunkCoordinates.raw_chunk(UtilChunkCoordinates.tile_at(world.player_position(player_id)), world.chunks().chunk_size)


## 返回玩家所在的规范区块。
static func current_canonical_chunk(world: WorldState, player_id: int) -> Vector2i:
	return UtilChunkCoordinates.canonical_chunk(current_raw_chunk(world, player_id), world.chunks().world_size)


## 按当前、已加载或未加载状态选择轮廓类别。
static func chunk_state(world: WorldState, player_id: int, raw: Vector2i) -> int:
	if raw == current_raw_chunk(world, player_id):
		return 2
	return 1 if world.chunks().loaded.has(raw) else 0


## 判断区块东侧是否为世界回环接缝。
static func wrap_east(world: WorldState, raw: Vector2i) -> bool:
	return posmod(raw.x + 1, world.chunks().world_size.x) == 0


## 判断区块南侧是否为世界回环接缝。
static func wrap_south(world: WorldState, raw: Vector2i) -> bool:
	return posmod(raw.y + 1, world.chunks().world_size.y) == 0


## 返回行走区块对应的规范坐标标签。
static func chunk_label(world: WorldState, raw: Vector2i) -> String:
	var canonical := UtilChunkCoordinates.canonical_chunk(raw, world.chunks().world_size)
	return "%d,%d" % [canonical.x, canonical.y]


## 返回固定在雷达中心的玩家标记位置。
static func player_marker_position() -> Vector2:
	return RADAR_CENTER


## 读取玩家朝向并转为雷达方向向量。
static func facing_vector(world: WorldState, player_id: int) -> Vector2:
	return Vector2(world.player_facing(player_id)).normalized()


## 避开玩家标记安排当前区块坐标标签。
static func current_label_position(world: WorldState, player_id: int) -> Vector2:
	var corners := chunk_corners(world, player_id, current_raw_chunk(world, player_id))
	var center := (corners[0] + corners[2]) * 0.5
	var below := RADAR_CENTER.y < center.y or (is_equal_approx(RADAR_CENTER.y, center.y) and facing_vector(world, player_id).y < 0)
	return center + Vector2(-12, 14 if below else -4)
