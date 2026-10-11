## 提供区块边缘和雷达的调试开关。
class_name DebugPanel
extends PanelContainer

## 切换区块边缘线的按钮。
@onready var borders_button: CheckButton = $Options/BordersButton
## 切换雷达显示的按钮。
@onready var radar_button: CheckButton = $Options/RadarButton

## 调试面板读取和更新的世界状态。
var source: WorldState


## 绑定数据源并连接开关事件。
func bind(world: WorldState) -> void:
	if source != null:
		source.debug_changed.disconnect(_on_debug_changed)
	source = world
	source.debug_changed.connect(_on_debug_changed)
	if not borders_button.toggled.is_connected(_on_borders_toggled):
		borders_button.toggled.connect(_on_borders_toggled)
	if not radar_button.toggled.is_connected(_on_radar_toggled):
		radar_button.toggled.connect(_on_radar_toggled)
	_sync_buttons()


## 在调试设置变化时同步开关状态。
func _on_debug_changed() -> void:
	_sync_buttons()


## 从数据源更新两个开关，避免回写事件。
func _sync_buttons() -> void:
	borders_button.set_pressed_no_signal(source.debug().edges)
	radar_button.set_pressed_no_signal(source.debug().radar)


## 将区块边缘开关写入世界数据。
func _on_borders_toggled(enabled: bool) -> void:
	source.set_debug_edges(enabled)


## 将雷达开关写入世界数据。
func _on_radar_toggled(enabled: bool) -> void:
	source.set_debug_radar(enabled)
