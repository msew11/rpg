## 为测试提供内存中的数据源替身。
extends DataSource

var _values: Dictionary = {}


## 检查测试键是否已写入。
func has_value(key: StringName) -> bool:
	return _values.has(key)


## 读取测试键的当前值。
func read_value(key: StringName) -> Variant:
	return _values.get(key)


## 更新测试键并仅对有效变化发出通知。
func write_value(key: StringName, value: Variant) -> bool:
	if _values.has(key) and _values[key] == value:
		return false
	_values[key] = value
	value_changed.emit(key, value)
	return true
