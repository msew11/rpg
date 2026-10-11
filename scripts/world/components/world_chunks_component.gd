## 保存世界尺寸、全部规范区块数据和当前展示状态。
class_name WorldChunksComponent
extends EcsComponent

## 单个区块的地块尺寸。
var chunk_size := Vector2i.ZERO
## 有限世界的规范区块尺寸。
var world_size := Vector2i.ZERO
## 当前需要显示的行走区块坐标。
var loaded: Array[Vector2i] = []
## 规范区块 ID 到地块数据的索引。
var _tiles_by_id: Dictionary = {}


## 检查规范区块的数据是否已创建。
func has_chunk(canonical_id: Vector2i) -> bool:
	return _tiles_by_id.has(canonical_id)


## 读取区块地块数组的副本，避免调用方直接改动世界状态。
func tiles_for(canonical_id: Vector2i) -> PackedByteArray:
	if not has_chunk(canonical_id):
		return PackedByteArray()
	var tiles: PackedByteArray = _tiles_by_id[canonical_id]
	return tiles.duplicate()


## 保存区块地块数组的副本，仅在内容变化时返回成功。
func set_tiles(canonical_id: Vector2i, tiles: PackedByteArray) -> bool:
	if has_chunk(canonical_id) and _tiles_by_id[canonical_id] == tiles:
		return false
	_tiles_by_id[canonical_id] = tiles.duplicate()
	return true
