extends SceneTree

const MemoryDataSource = preload("res://tests/doubles/memory_data_source.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	var source: DataSource = MemoryDataSource.new()
	var events: Array[Array] = []
	source.value_changed.connect(func(key: StringName, value: Variant) -> void:
		events.append([key, value])
	)

	_check(not source.has_value(&"missing"), "unwritten key is absent")
	_check(source.read_value(&"missing") == null, "absent read returns null")
	_check(source.write_value(&"nullable", null), "creating null is a change")
	_check(source.has_value(&"nullable"), "stored null is present")
	_check(source.read_value(&"nullable") == null, "stored null reads as null")
	_check(events.size() == 1 and events[0] == [&"nullable", null], "creation emits key and value")

	_check(source.write_value(&"score", 1), "first write changes value")
	_check(source.write_value(&"score", 2), "replacement changes value")
	_check(source.read_value(&"score") == 2, "read returns latest value")
	_check(events.size() == 3 and events[1] == [&"score", 1] and events[2] == [&"score", 2], "each effective write emits once")
	_check(not source.write_value(&"score", 2), "equal-value write returns false")
	_check(not source.write_value(&"nullable", null), "equal null is unchanged")
	_check(events.size() == 3, "equal-value writes emit nothing")

	if _failures.is_empty():
		print("Data source contract passed.")
		quit(0)
	else:
		for failure in _failures:
			printerr(failure)
		quit(1)


func _check(condition: bool, description: String) -> void:
	if not condition:
		_failures.append(description)
