## 提供世界地块、行走区块与回环区块之间的坐标转换。
class_name ChunkCoordinates
extends RefCounted


## 将连续世界位置映射到所在的整数地块。
static func tile_at(world_position: Vector2) -> Vector2i:
	return Vector2i(floori(world_position.x), floori(world_position.y))


## 求地块所在的行走区块，兼容负坐标。
static func raw_chunk(tile: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(
		floori(float(tile.x) / chunk_size.x),
		floori(float(tile.y) / chunk_size.y)
	)


## 求地块在区块内的非负坐标。
static func local_tile(tile: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(posmod(tile.x, chunk_size.x), posmod(tile.y, chunk_size.y))


## 将行走区块映射到有限世界中的规范区块。
static func canonical_chunk(raw: Vector2i, world_size: Vector2i) -> Vector2i:
	return Vector2i(posmod(raw.x, world_size.x), posmod(raw.y, world_size.y))


## 求行走区块在世界中的地块原点。
static func tile_origin(raw: Vector2i, chunk_size: Vector2i) -> Vector2i:
	return Vector2i(raw.x * chunk_size.x, raw.y * chunk_size.y)
