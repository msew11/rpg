# 为 openspec/specs/data-access/spec.md 的数据访问契约测试提供内存替身。
class_name DoubleMemoryDataSource
extends DataSource

var _values: Dictionary = {}


# 报告测试替身中是否已创建指定键。
func has_value(key: StringName) -> bool:
	return _values.has(key)


# 读取测试替身保存的值，缺失键返回空值。
func read_value(key: StringName) -> Variant:
	return _values.get(key)


# 保存变化后的值并发出一次通知，相等值不触发变化。
func write_value(key: StringName, value: Variant) -> bool:
	if _values.has(key) and _values[key] == value:
		return false
	_values[key] = value
	value_changed.emit(key, value)
	return true
