class_name WorldState
extends DataSource

const LOADED_CHUNKS_KEY := &"world/loaded_chunks"
const CHUNK_SIZE_KEY := &"world/chunk_size"
const WORLD_SIZE_KEY := &"world/world_size"
const DEBUG_EDGES_KEY := &"debug/chunk_edges"
const DEBUG_RADAR_KEY := &"debug/radar"

var ecs := EcsWorld.new()
var _values: Dictionary = {}
var _entity_fields: Dictionary = {}


func _init() -> void:
	ecs.add_system(MovementSystem.new())


func create_entity(position: Vector2, speed: float) -> int:
	var entity_id := ecs.create_entity()
	ecs.add_component(entity_id, PositionComponent.new(position))
	ecs.add_component(entity_id, MovementIntentComponent.new())
	ecs.add_component(entity_id, MoveSpeedComponent.new(speed))
	ecs.add_component(entity_id, FacingComponent.new())
	_entity_fields[position_key(entity_id)] = [entity_id, PositionComponent]
	_entity_fields[intent_key(entity_id)] = [entity_id, MovementIntentComponent]
	_entity_fields[speed_key(entity_id)] = [entity_id, MoveSpeedComponent]
	_entity_fields[facing_key(entity_id)] = [entity_id, FacingComponent]
	for key in [position_key(entity_id), intent_key(entity_id), speed_key(entity_id), facing_key(entity_id)]:
		value_changed.emit(key, read_value(key))
	return entity_id


func step(delta: float) -> void:
	var previous: Dictionary = {}
	for key in _entity_fields:
		var entity_id: int = _entity_fields[key][0]
		if key == position_key(entity_id) or key == facing_key(entity_id):
			previous[key] = read_value(key)
	ecs.step(delta)
	for key in previous:
		var current: Variant = read_value(key)
		if current != previous[key]:
			value_changed.emit(key, current)


func has_value(key: StringName) -> bool:
	if _entity_fields.has(key):
		var field: Array = _entity_fields[key]
		return ecs.has_component(field[0], field[1])
	return _values.has(key)


func read_value(key: StringName) -> Variant:
	if _entity_fields.has(key):
		var field: Array = _entity_fields[key]
		var component := ecs.get_component(field[0], field[1])
		return _copy_value(component.get(&"value")) if component != null else null
	return _copy_value(_values.get(key))


func write_value(key: StringName, value: Variant) -> bool:
	var stored: Variant = _copy_value(value)
	if _entity_fields.has(key):
		var field: Array = _entity_fields[key]
		var component := ecs.get_component(field[0], field[1])
		if component == null or component.get(&"value") == stored:
			return false
		component.set(&"value", stored)
	elif _values.has(key) and _values[key] == stored:
		return false
	else:
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
