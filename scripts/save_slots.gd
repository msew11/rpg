## 管理五个本地 JSON 存档槽位及其完整性检查。
class_name SaveSlots
extends RefCounted

## 可供玩家选择的存档槽数量。
const SLOT_COUNT := 5
## 当前测试地图存档格式版本。
const FORMAT_VERSION := 1
## 默认存档目录。
const DEFAULT_DIR := "user://saves"

## 当前实例使用的存档目录，测试可传入独立目录。
var save_dir: String


## 使用指定目录管理槽位。
func _init(directory: String = DEFAULT_DIR) -> void:
	save_dir = directory


## 返回槽位的空、有效或无效状态及保存时间。
func inspect(slot: int) -> Dictionary:
	if not _valid_slot(slot):
		return {"state": "invalid"}
	var path := _slot_path(slot)
	if not FileAccess.file_exists(path):
		return {"state": "empty"}
	var snapshot := read(slot)
	if snapshot.is_empty():
		return {"state": "invalid"}
	return {"state": "valid", "saved_at": snapshot["saved_at"]}


## 将当前会话状态覆盖保存到指定槽位。
func save(slot: int, session: LocalSession) -> bool:
	if not _valid_slot(slot) or session == null:
		return false
	var snapshot := session.capture_snapshot()
	snapshot["version"] = FORMAT_VERSION
	snapshot["saved_at"] = Time.get_datetime_string_from_system()
	if not session.working_store.replace_snapshot(snapshot):
		return false
	var absolute_dir := ProjectSettings.globalize_path(save_dir)
	if DirAccess.make_dir_recursive_absolute(absolute_dir) != OK:
		return false
	var temporary := _slot_path(slot) + ".new"
	var file := FileAccess.open(temporary, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(snapshot))
	file.flush()
	var written := file.get_error() == OK
	file.close()
	if not written:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return false
	if DirAccess.rename_absolute(ProjectSettings.globalize_path(temporary), ProjectSettings.globalize_path(_slot_path(slot))) != OK:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return false
	return true


## 读取并完整验证槽位内容；无效时返回空字典。
func read(slot: int) -> Dictionary:
	if not _valid_slot(slot):
		return {}
	var path := _slot_path(slot)
	if not FileAccess.file_exists(path):
		return {}
	var parser := JSON.new()
	if parser.parse(FileAccess.get_file_as_string(path)) != OK:
		return {}
	var parsed: Variant = parser.data
	if not _valid_snapshot(parsed):
		return {}
	return parsed


## 生成固定编号的存档路径。
func _slot_path(slot: int) -> String:
	return "%s/slot_%d.json" % [save_dir, slot]


## 判断槽位编号是否属于固定的五个槽位。
func _valid_slot(slot: int) -> bool:
	return slot >= 1 and slot <= SLOT_COUNT


## 验证存档全部字段和当前手工测试地图尺寸。
func _valid_snapshot(value: Variant) -> bool:
	if not value is Dictionary:
		return false
	var data: Dictionary = value
	if data.get("version") != FORMAT_VERSION or not data.get("saved_at") is String:
		return false
	if not _valid_number_pair(data.get("position"), false):
		return false
	if not _valid_number_pair(data.get("facing"), true):
		return false
	var facing: Array = data["facing"]
	if int(facing[0]) == 0 and int(facing[1]) == 0:
		return false
	var config := ManualChunkCfg.new()
	if not _valid_size(data.get("chunk_size"), config.chunk_size):
		return false
	if not _valid_size(data.get("world_size"), config.world_size):
		return false
	if not data.get("changed_chunks") is Array:
		return false
	var seen := {}
	for entry in data["changed_chunks"]:
		if not entry is Dictionary or not entry.get("id") is Array or not entry.get("tiles") is Array:
			return false
		var id: Array = entry["id"]
		var tiles: Array = entry["tiles"]
		if id.size() != 2 or not _is_integer(id[0]) or not _is_integer(id[1]):
			return false
		var x := int(id[0])
		var y := int(id[1])
		if x < 0 or y < 0 or x >= config.world_size.x or y >= config.world_size.y:
			return false
		var key := Vector2i(x, y)
		if seen.has(key) or tiles.size() != config.chunk_size.x * config.chunk_size.y:
			return false
		seen[key] = true
		for tile in tiles:
			if not _is_integer(tile) or (int(tile) != 0 and int(tile) != 1):
				return false
		if entry.has("entities") and not WorldState.can_restore_entities(JSON.stringify(entry["entities"])):
			return false
	return true


## 判断二维字段是有限数字，朝向还需是单位离散方向。
func _valid_number_pair(value: Variant, direction: bool) -> bool:
	if not value is Array or value.size() != 2:
		return false
	for part in value:
		if not (part is int or part is float) or not is_finite(float(part)):
			return false
		if direction and (not _is_integer(part) or absi(int(part)) > 1):
			return false
	return true


## 判断地图尺寸字段与当前配置完全一致。
func _valid_size(value: Variant, expected: Vector2i) -> bool:
	return value is Array and value.size() == 2 and _is_integer(value[0]) and _is_integer(value[1]) and int(value[0]) == expected.x and int(value[1]) == expected.y


## 判断 JSON 数值是可安全转换的整数。
func _is_integer(value: Variant) -> bool:
	return (value is int or value is float) and is_finite(float(value)) and float(value) == floorf(float(value))
