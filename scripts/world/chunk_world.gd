## 管理玩家附近的测试区块及其运行期地块状态。
class_name ChunkWorld
extends RefCounted

const LOAD_RADIUS := 1

var source: WorldState
var catalog: TestChunkCatalog
var _last_raw_chunk := Vector2i.ZERO
var _loaded_once := false


## 绑定世界实体与测试目录，并设置区块尺寸。
func _init(world: WorldState, test_catalog: TestChunkCatalog) -> void:
	source = world
	catalog = test_catalog
	source.chunks().chunk_size = catalog.chunk_size
	source.chunks().world_size = catalog.world_size


## 按玩家位置更新附近的行走区块显示范围。
func load_around(world_position: Vector2) -> void:
	var tile := ChunkCoordinates.tile_at(world_position)
	var center := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	if _loaded_once and center == _last_raw_chunk:
		return
	_last_raw_chunk = center
	_loaded_once = true
	var loaded: Array[Vector2i] = []
	for y in range(center.y - LOAD_RADIUS, center.y + LOAD_RADIUS + 1):
		for x in range(center.x - LOAD_RADIUS, center.x + LOAD_RADIUS + 1):
			var raw := Vector2i(x, y)
			ensure_chunk(ChunkCoordinates.canonical_chunk(raw, catalog.world_size))
			loaded.append(raw)
	source.set_loaded_chunks(loaded)


## 首次访问规范区块时载入其测试地块。
func ensure_chunk(canonical_id: Vector2i) -> void:
	if not source.chunks().has_chunk(canonical_id):
		source.set_chunk_tiles(canonical_id, catalog.tiles_for(canonical_id))


## 为完整地图准备全部规范区块数据。
func ensure_all_chunks() -> void:
	for y in range(catalog.world_size.y):
		for x in range(catalog.world_size.x):
			ensure_chunk(Vector2i(x, y))


## 读取指定世界地块的当前类型。
func tile_type(tile: Vector2i) -> int:
	var raw := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	var canonical := ChunkCoordinates.canonical_chunk(raw, catalog.world_size)
	ensure_chunk(canonical)
	var tiles := source.chunks().tiles_for(canonical)
	var local := ChunkCoordinates.local_tile(tile, catalog.chunk_size)
	return tiles[local.y * catalog.chunk_size.x + local.x]


## 切换玩家所在地块的类型并保存到世界实体。
func toggle_tile(world_position: Vector2) -> void:
	var tile := ChunkCoordinates.tile_at(world_position)
	var raw := ChunkCoordinates.raw_chunk(tile, catalog.chunk_size)
	var canonical := ChunkCoordinates.canonical_chunk(raw, catalog.world_size)
	ensure_chunk(canonical)
	var tiles := source.chunks().tiles_for(canonical)
	var local := ChunkCoordinates.local_tile(tile, catalog.chunk_size)
	var index := local.y * catalog.chunk_size.x + local.x
	tiles[index] = 1 - tiles[index]
	source.set_chunk_tiles(canonical, tiles)
