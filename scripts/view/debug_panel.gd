class_name DebugPanel
extends PanelContainer

@onready var borders_button: CheckButton = $Options/BordersButton
@onready var radar_button: CheckButton = $Options/RadarButton

var source: DataSource


func bind(data_source: DataSource) -> void:
	source = data_source
	source.value_changed.connect(_on_value_changed)
	borders_button.toggled.connect(_on_borders_toggled)
	radar_button.toggled.connect(_on_radar_toggled)
	_sync_buttons()


func _on_value_changed(key: StringName, _value: Variant) -> void:
	if key == WorldState.DEBUG_EDGES_KEY or key == WorldState.DEBUG_RADAR_KEY:
		_sync_buttons()


func _sync_buttons() -> void:
	borders_button.set_pressed_no_signal(source.read_value(WorldState.DEBUG_EDGES_KEY) == true)
	radar_button.set_pressed_no_signal(source.read_value(WorldState.DEBUG_RADAR_KEY) == true)


func _on_borders_toggled(enabled: bool) -> void:
	source.write_value(WorldState.DEBUG_EDGES_KEY, enabled)


func _on_radar_toggled(enabled: bool) -> void:
	source.write_value(WorldState.DEBUG_RADAR_KEY, enabled)
