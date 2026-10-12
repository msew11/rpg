## 提供区块边缘、雷达、视野、怪物创建和存档目录的调试操作。
class_name DebugPanel
extends PanelContainer

## 视野高度倍数变化时通知主镜头。
signal view_height_changed(multiplier: float)

## 请求在玩家当前区块创建一个怪物实体。
signal add_monster_requested

## 切换区块边缘线的按钮。
@onready var borders_button: CheckButton = $Options/BordersButton
## 切换雷达显示的按钮。
@onready var radar_button: CheckButton = $Options/RadarButton

## 显示当前视野高度倍数。
@onready var view_height_label: Label = $Options/ViewHeightLabel

## 调整镜头可见世界高度的滑条。
@onready var view_height_slider: HSlider = $Options/ViewHeightSlider

## 在当前区块创建怪物的按钮。
@onready var add_monster_button: Button = $Options/AddMonsterButton

## 显示可选中复制的正式存档目录。
@onready var save_dir_path: LineEdit = $Options/SaveDirPath

## 调试面板读取的世界状态。
var source: WorldManager

## 提交调试交互的当前本地会话。
var interaction_session: LocalSession


## 绑定当前会话及其只读世界状态，并连接开关事件。
func bind(session: LocalSession) -> void:
	if source != null:
		source.debug_changed.disconnect(_on_debug_changed)
	interaction_session = session
	source = session.world_manager
	source.debug_changed.connect(_on_debug_changed)
	if not borders_button.toggled.is_connected(_on_borders_toggled):
		borders_button.toggled.connect(_on_borders_toggled)
	if not radar_button.toggled.is_connected(_on_radar_toggled):
		radar_button.toggled.connect(_on_radar_toggled)
	if not view_height_slider.value_changed.is_connected(_on_view_height_changed):
		view_height_slider.value_changed.connect(_on_view_height_changed)
	if not add_monster_button.pressed.is_connected(_on_add_monster_pressed):
		add_monster_button.pressed.connect(_on_add_monster_pressed)
	_sync_buttons()
	_on_view_height_changed(view_height_slider.value)


## 在调试设置变化时同步开关状态。
func _on_debug_changed() -> void:
	_sync_buttons()


## 从数据源更新两个开关，避免回写事件。
func _sync_buttons() -> void:
	borders_button.set_pressed_no_signal(source.debug_view().edges)
	radar_button.set_pressed_no_signal(source.debug_view().radar)


## 经当前会话提交区块边缘开关。
func _on_borders_toggled(enabled: bool) -> void:
	interaction_session.set_debug_edges(enabled)


## 经当前会话提交雷达开关。
func _on_radar_toggled(enabled: bool) -> void:
	interaction_session.set_debug_radar(enabled)


## 更新数值提示并通知镜头改变可见高度。
func _on_view_height_changed(multiplier: float) -> void:
	view_height_label.text = "视野高度 %.2f×" % multiplier
	view_height_changed.emit(multiplier)


## 将创建请求交给当前游玩会话。
func _on_add_monster_pressed() -> void:
	add_monster_requested.emit()


## 显示当前存档管理器实际使用的绝对目录。
func set_save_directory(directory: String) -> void:
	save_dir_path.text = directory
	save_dir_path.tooltip_text = directory
