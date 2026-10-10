extends DataSource

var _values: Dictionary = {}


func has_value(key: StringName) -> bool:
	return _values.has(key)


func read_value(key: StringName) -> Variant:
	return _values.get(key)


func write_value(key: StringName, value: Variant) -> bool:
	if _values.has(key) and _values[key] == value:
		return false
	_values[key] = value
	value_changed.emit(key, value)
	return true
