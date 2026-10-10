class_name WorldState
extends DataSource

const LOADED_CHUNKS_KEY := &"world/loaded_chunks"
const CHUNK_SIZE_KEY := &"world/chunk_size"
const WORLD_SIZE_KEY := &"world/world_size"

var entity_ids: Array[int] = []
var _values: Dictionary = {}
var _next_entity_id := 1


func create_entity(position: Vector2, speed: float) -> int:
	var entity_id := _next_entity_id
	_next_entity_id += 1
	entity_ids.append(entity_id)
	write_value(position_key(entity_id), position)
	write_value(intent_key(entity_id), Vector2i.ZERO)
	write_value(speed_key(entity_id), speed)
	write_value(facing_key(entity_id), Vector2i(0, 1))
	return entity_id


func has_value(key: StringName) -> bool:
	return _values.has(key)


func read_value(key: StringName) -> Variant:
	return _copy_value(_values.get(key))


func write_value(key: StringName, value: Variant) -> bool:
	var stored: Variant = _copy_value(value)
	if _values.has(key) and _values[key] == stored:
		return false
	_values[key] = stored
	value_changed.emit(key, _copy_value(stored))
	return true


static func position_key(entity_id: int) -> StringName:
	return StringName("entity/%d/position" % entity_id)


static func intent_key(entity_id: int) -> StringName:
	return StringName("entity/%d/intent" % entity_id)


static func speed_key(entity_id: int) -> StringName:
	return StringName("entity/%d/speed" % entity_id)


static func facing_key(entity_id: int) -> StringName:
	return StringName("entity/%d/facing" % entity_id)


static func chunk_key(canonical_id: Vector2i) -> StringName:
	return StringName("chunk/%d/%d" % [canonical_id.x, canonical_id.y])


static func _copy_value(value: Variant) -> Variant:
	if value is Dictionary or value is Array:
		return value.duplicate(true)
	if value is PackedByteArray:
		return value.duplicate()
	return value
