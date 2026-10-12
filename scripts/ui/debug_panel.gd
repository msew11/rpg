## 提供可移动的 GM 工具、分页和当前会话的调试操作。
class_name DebugPanel
extends PanelContainer

## 视野高度倍数变化时通知主镜头。
signal view_height_changed(multiplier: float)
## 请求在玩家当前区块创建怪物实体。
signal add_monster_requested
## 玩家显示模板变化时通知主场景。
signal player_template_changed(template_id: int)
## 动作演示变化时通知主场景。
signal action_changed(action_id: int)

## 可拖动的 GM 入口按钮。
@onready var gm_button: Button = $"../GmButton"
## 固定面板的按钮。
@onready var pin_button: Button = $Content/Header/PinButton
## 面板的拖动区域。
@onready var drag_handle: Button = $Content/Header/DragHandle
## 按用途组织功能的分页。
@onready var tabs: TabContainer = $Content/Tabs
## 切换区块边缘线的按钮。
@onready var borders_button: CheckButton = $Content/Tabs/World/BordersButton
## 显示当前视野高度倍数。
@onready var view_height_label: Label = $Content/Tabs/World/ViewHeightLabel
## 调整镜头可见世界高度的滑条。
@onready var view_height_slider: HSlider = $Content/Tabs/World/ViewHeightSlider
## 在当前区块创建怪物的按钮。
@onready var add_monster_button: Button = $Content/Tabs/World/AddMonsterButton
## 调试用角色模板选择框。
@onready var player_template_selector: OptionButton = $Content/Tabs/Character/PlayerTemplateSelector
## 要演示的角色动作选择框。
@onready var action_selector: OptionButton = $Content/Tabs/Character/ActionSelector
## 显示可选中复制的正式存档目录。
@onready var save_dir_path: LineEdit = $Content/Tabs/Save/SaveDirPath

## 调试面板读取的世界状态。
var source: WorldManager
## 提交调试交互的当前本地会话。
var interaction_session: LocalSession
## 当前正在拖动的控件。
var drag_target: Control
## 按下鼠标时的屏幕坐标。
var drag_start_mouse := Vector2.ZERO
## 按下鼠标时控件的屏幕坐标。
var drag_start_position := Vector2.ZERO
## 本次按钮操作是否超过点击距离。
var dragged := false


## 初始化分页、拖动和角色选项。
func _ready() -> void:
	tabs.set_tab_title(0, "世界")
	tabs.set_tab_title(1, "角色")
	tabs.set_tab_title(2, "存档")
	gm_button.gui_input.connect(_on_gm_button_input)
	drag_handle.gui_input.connect(_on_drag_handle_input)
	pin_button.toggled.connect(_on_pin_toggled)
	get_viewport().size_changed.connect(_clamp_controls)
	player_template_selector.add_item("原占位角色", PlayerView.TEMPLATE_PLACEHOLDER)
	player_template_selector.add_item("ErisEsra 16×16 素体", PlayerView.TEMPLATE_SMALL)
	player_template_selector.add_item("ErisEsra 16×32 素体", PlayerView.TEMPLATE_TALL)
	player_template_selector.select(PlayerView.TEMPLATE_TALL)
	player_template_selector.item_selected.connect(_on_player_template_selected)
	for item in [["跟随角色", PlayerView.ACTION_DEFAULT], ["待机", PlayerView.ACTION_IDLE], ["行走", PlayerView.ACTION_WALK], ["奔跑", PlayerView.ACTION_RUN], ["攻击", PlayerView.ACTION_ATTACK], ["交互", PlayerView.ACTION_INTERACT], ["跳跃", PlayerView.ACTION_JUMP], ["转身", PlayerView.ACTION_ROTATE]]:
		action_selector.add_item(item[0], item[1])
	action_selector.select(0)
	action_selector.item_selected.connect(_on_action_selected)


## 记录入口按钮的按下位置。
func _on_gm_button_input(event: InputEvent) -> void:
	_start_drag(event, gm_button)


## 记录面板标题栏的按下位置。
func _on_drag_handle_input(event: InputEvent) -> void:
	_start_drag(event, self)


## 开始拖动指定控件。
func _start_drag(event: InputEvent, target: Control) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		drag_target = target
		drag_start_mouse = event.global_position
		drag_start_position = target.position
		dragged = false


## 处理拖动、入口点击和面板外点击。
func _input(event: InputEvent) -> void:
	if drag_target != null:
		if event is InputEventMouseMotion and event.button_mask & MOUSE_BUTTON_MASK_LEFT:
			var delta: Vector2 = event.global_position - drag_start_mouse
			if delta.length() > 5.0:
				dragged = true
			if dragged:
				drag_target.position = _clamped_position(drag_start_position + delta, drag_target.size)
		elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
			var open_button := drag_target == gm_button and not dragged and gm_button.get_global_rect().has_point(event.global_position)
			drag_target = null
			if open_button:
				open_panel()
			return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed and visible and not pin_button.button_pressed and not get_global_rect().has_point(event.global_position):
		close_panel()


## 展开 GM 面板并隐藏入口。
func open_panel() -> void:
	visible = true
	gm_button.visible = false
	position = _clamped_position(position, size)


## 收起 GM 面板并恢复入口。
func close_panel() -> void:
	visible = false
	gm_button.visible = true


## 更新固定按钮的文字。
func _on_pin_toggled(enabled: bool) -> void:
	pin_button.text = "取消固定" if enabled else "钉住"


## 将两个 GM 控件保持在视口范围内。
func _clamp_controls() -> void:
	position = _clamped_position(position, size)
	gm_button.position = _clamped_position(gm_button.position, gm_button.size)


## 返回控件在当前视口内的位置。
func _clamped_position(candidate: Vector2, control_size: Vector2) -> Vector2:
	return candidate.clamp(Vector2.ZERO, (get_viewport_rect().size - control_size).max(Vector2.ZERO))


## 返回当前选择的角色模板编号。
func selected_player_template() -> int:
	return player_template_selector.get_selected_id()


## 切换模板时停止动作演示。
func _on_player_template_selected(index: int) -> void:
	action_selector.select(0)
	action_selector.disabled = player_template_selector.get_item_id(index) == PlayerView.TEMPLATE_PLACEHOLDER
	action_changed.emit(PlayerView.ACTION_DEFAULT)
	player_template_changed.emit(player_template_selector.get_item_id(index))


## 将动作选择转交给当前玩家显示节点。
func _on_action_selected(index: int) -> void:
	action_changed.emit(action_selector.get_item_id(index))


## 在角色状态切换时同步默认动作选择。
func stop_action_demo() -> void:
	action_selector.select(0)


## 绑定当前会话及其只读世界状态。
func bind(session: LocalSession) -> void:
	if source != null:
		source.debug_changed.disconnect(_on_debug_changed)
	interaction_session = session
	source = session.world_manager
	source.debug_changed.connect(_on_debug_changed)
	if not borders_button.toggled.is_connected(_on_borders_toggled):
		borders_button.toggled.connect(_on_borders_toggled)
	if not view_height_slider.value_changed.is_connected(_on_view_height_changed):
		view_height_slider.value_changed.connect(_on_view_height_changed)
	if not add_monster_button.pressed.is_connected(_on_add_monster_pressed):
		add_monster_button.pressed.connect(_on_add_monster_pressed)
	_sync_buttons()
	_on_view_height_changed(view_height_slider.value)


## 在调试设置变化时同步开关状态。
func _on_debug_changed() -> void:
	_sync_buttons()


## 从数据源更新边缘开关，避免回写事件。
func _sync_buttons() -> void:
	borders_button.set_pressed_no_signal(source.debug_view().edges)


## 经当前会话提交区块边缘开关。
func _on_borders_toggled(enabled: bool) -> void:
	interaction_session.set_debug_edges(enabled)


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
