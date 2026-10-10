## 从手工测试地图读取区块布局与地块图案。
class_name TestChunkCatalog
extends RefCounted

const SOURCE_PATH := "res://data/test_chunks.json"

var chunk_size: Vector2i
var world_size: Vector2i
var _layout: Array
var _patterns: Dictionary


## 加载并校验测试地图的尺寸、布局和图案。
func _init() -> void:
	var parsed: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(SOURCE_PATH))
	chunk_size = Vector2i(parsed["chunk_size"][0], parsed["chunk_size"][1])
	_layout = parsed["layout"]
	_patterns = parsed["patterns"]
	world_size = Vector2i(_layout[0].size(), _layout.size())
	assert(chunk_size.x > 0 and chunk_size.y > 0)
	for row in _layout:
		assert(row.size() == world_size.x)
		for pattern_name in row:
			assert(_patterns.has(pattern_name))
	for rows in _patterns.values():
		assert(rows.size() == chunk_size.y)
		for row in rows:
			assert(row.length() == chunk_size.x)


## 生成指定规范区块的地块类型数组。
func tiles_for(canonical_id: Vector2i) -> PackedByteArray:
	assert(canonical_id.x >= 0 and canonical_id.x < world_size.x)
	assert(canonical_id.y >= 0 and canonical_id.y < world_size.y)
	var pattern_name: String = _layout[canonical_id.y][canonical_id.x]
	var rows: Array = _patterns[pattern_name]
	var tiles := PackedByteArray()
	tiles.resize(chunk_size.x * chunk_size.y)
	for y in range(chunk_size.y):
		for x in range(chunk_size.x):
			tiles[y * chunk_size.x + x] = 1 if rows[y].substr(x, 1) == "#" else 0
	return tiles
