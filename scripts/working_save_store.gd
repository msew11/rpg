## 管理一个会话的临时增量存档，供缓存按规范区块读写。
class_name WorkingSaveStore
extends RefCounted

## 工作存档默认路径。
const DEFAULT_PATH := "user://saves/current.tmp.json"

## 当前工作文件路径。
var path: String
## 已落地的会话数据。
var snapshot: Dictionary
## 测试用的写入失败开关。
var fail_writes := false


## 清理上次残留，并从初始或正式快照建立本会话工作副本。
func _init(initial: Dictionary = {}, file_path: String = DEFAULT_PATH) -> void:
	path = file_path
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	if FileAccess.file_exists(path + ".new"):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path + ".new"))
	snapshot = initial.duplicate(true)
	if not snapshot.has("changed_chunks"):
		snapshot["changed_chunks"] = []
	_write()


## 用规范区块 ID 查找最近成功落地的数据，缺失时返回基准地块。
func load_chunk(canonical_id: Vector2i, baseline: PackedByteArray) -> PackedByteArray:
	for entry: Dictionary in snapshot["changed_chunks"]:
		if _same_id(entry["id"], canonical_id):
			return PackedByteArray(entry["tiles"])
	return baseline.duplicate()


## 将区块相对基准的最新状态替换进临时存档。
func unload_chunk(canonical_id: Vector2i, tiles: PackedByteArray, baseline: PackedByteArray, entities_json: String = "{}") -> bool:
	var next := snapshot.duplicate(true)
	merge_chunk_into(next, canonical_id, tiles, baseline, entities_json)
	if next["changed_chunks"] == snapshot["changed_chunks"]:
		return true
	if not _write(next):
		return false
	snapshot = next
	return true


## 合并活跃数据后的完整工作快照写入临时存档。
func replace_snapshot(value: Dictionary) -> bool:
	if not _write(value):
		return false
	snapshot = value.duplicate(true)
	return true


## 在快照内替换一个规范区块的差量记录。
func merge_chunk_into(value: Dictionary, canonical_id: Vector2i, tiles: PackedByteArray, baseline: PackedByteArray, entities_json: String = "{}") -> void:
	var entries: Array = value["changed_chunks"]
	var key := [canonical_id.x, canonical_id.y]
	var entities: Dictionary = JSON.parse_string(entities_json)
	for index in range(entries.size() - 1, -1, -1):
		if _same_id(entries[index]["id"], canonical_id):
			entries.remove_at(index)
	if tiles != baseline or not entities.is_empty():
		entries.append({"id": key, "tiles": Array(tiles), "entities": entities})


## 读取区块拥有的子实体树；没有记录时返回空数组字符串。
func load_entities_json(canonical_id: Vector2i) -> String:
	for entry: Dictionary in snapshot["changed_chunks"]:
		if _same_id(entry["id"], canonical_id):
			return JSON.stringify(entry.get("entities", []))
	return "{}"


## 比较 JSON 数字和运行时整数构成的规范区块标识。
func _same_id(value: Array, canonical_id: Vector2i) -> bool:
	return value.size() == 2 and int(value[0]) == canonical_id.x and int(value[1]) == canonical_id.y


## 写入当前或指定快照；失败时不改变内存快照。
func _write(value: Dictionary = {}) -> bool:
	if fail_writes:
		return false
	var contents := snapshot if value.is_empty() else value
	var directory := ProjectSettings.globalize_path(path.get_base_dir())
	if DirAccess.make_dir_recursive_absolute(directory) != OK:
		return false
	var temporary := path + ".new"
	var file := FileAccess.open(temporary, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(contents))
	file.flush()
	var written := file.get_error() == OK
	file.close()
	if not written:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return false
	if DirAccess.rename_absolute(ProjectSettings.globalize_path(temporary), ProjectSettings.globalize_path(path)) != OK:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return false
	return true
