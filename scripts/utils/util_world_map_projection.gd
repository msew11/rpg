## 按当前世界组件和界面参数计算完整地图位置。
class_name UtilWorldMapProjection
extends RefCounted

## 完整地图中一个区块轴向的屏幕像素长度。
const CHUNK_AXIS_PIXELS := 58.0


## 将世界位置映射到可平移的地图坐标。
static func map_point(world: WorldManager, map_size: Vector2, pan_offset: Vector2, world_position: Vector2) -> Vector2:
	var chunks := world.chunks_view()
	var relative := world_position / Vector2(chunks.chunk_size) - Vector2(chunks.world_size) / 2.0
	return map_size / 2.0 + pan_offset + Vector2(relative.x - relative.y, relative.x + relative.y) * CHUNK_AXIS_PIXELS


## 计算规范区块在地图上的四个顶点。
static func chunk_corners(world: WorldManager, map_size: Vector2, pan_offset: Vector2, canonical: Vector2i) -> PackedVector2Array:
	var chunk_size: Vector2i = world.chunks_view().chunk_size
	var origin := Vector2(UtilChunkCoordinates.tile_origin(canonical, chunk_size))
	return PackedVector2Array([
		map_point(world, map_size, pan_offset, origin),
		map_point(world, map_size, pan_offset, origin + Vector2(chunk_size.x, 0)),
		map_point(world, map_size, pan_offset, origin + Vector2(chunk_size)),
		map_point(world, map_size, pan_offset, origin + Vector2(0, chunk_size.y))
	])


## 将玩家位置回环到有限地图后定位标记。
static func player_marker_position(world: WorldManager, player_id: int, map_size: Vector2, pan_offset: Vector2) -> Vector2:
	var chunks := world.chunks_view()
	var period := Vector2(chunks.world_size * chunks.chunk_size)
	var position := world.player_position(player_id)
	return map_point(world, map_size, pan_offset, Vector2(fposmod(position.x, period.x), fposmod(position.y, period.y)))


## 返回玩家所在的规范区块。
static func current_chunk(world: WorldManager, player_id: int) -> Vector2i:
	var raw := UtilChunkCoordinates.raw_chunk(UtilChunkCoordinates.tile_at(world.player_position(player_id)), world.chunks_view().chunk_size)
	return UtilChunkCoordinates.canonical_chunk(raw, world.chunks_view().world_size)


## 判断规范区块是否属于当前加载范围。
static func is_loaded(world: WorldManager, canonical: Vector2i) -> bool:
	for raw in world.chunks_view().loaded:
		if UtilChunkCoordinates.canonical_chunk(raw, world.chunks_view().world_size) == canonical:
			return true
	return false
