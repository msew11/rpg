## 协调本地会话、区块、输入、玩家视图和镜头。
class_name Main
extends Node2D

## 用于重建玩家显示节点的场景。
const PLAYER_VIEW := preload("res://scenes/player_view.tscn")
## 当前显示标题主选项。
const MODE_TITLE := 0
## 当前正在游玩。
const MODE_PLAYING := 1
## 当前从标题读取存档。
const MODE_LOAD := 2
## 当前从游戏保存存档。
const MODE_SAVE := 3
## 默认镜头缩放，用作视野高度滑条的基准。
const DEFAULT_CAMERA_ZOOM := 1.4

## 持续跟随玩家的主镜头。
@onready var camera: Camera2D = $Camera2D
## 显示当前加载区块的地面节点。
@onready var ground: ChunkGround = $ChunkGround
## 显示玩家附近区块的雷达节点。
@onready var radar: ChunkRadar = $RadarLayer/ChunkRadar
## 控制调试显示的面板。
@onready var debug_panel: DebugPanel = $RadarLayer/DebugPanel
## 显示完整测试世界的地图节点。
@onready var world_map: WorldMap = $RadarLayer/WorldMap
## 显示会话系统消息的聊天面板。
@onready var chat_panel: ChatPanel = $RadarLayer/ChatPanel

## 接收输入并推进世界状态的本地会话。
var session := LocalSession.new()
## 当前玩家显示节点。
var player_view: Node2D
## 五槽位本地进度管理器。
var save_slots := SaveSlots.new()
## 当前菜单及游戏模式。
var mode := MODE_TITLE

## 标题或槽位菜单的全屏遮罩。
@onready var menu: Control = $MenuLayer/Menu
## 游戏内操作按钮组。
@onready var game_buttons: HBoxContainer = $RadarLayer/GameButtons
## 菜单标题。
@onready var heading: Label = $MenuLayer/Menu/Center/Panel/Content/Rows/Heading
## 菜单副标题。
@onready var subtitle: Label = $MenuLayer/Menu/Center/Panel/Content/Rows/Subtitle
## 标题页操作按钮组。
@onready var title_actions: VBoxContainer = $MenuLayer/Menu/Center/Panel/Content/Rows/TitleActions
## 槽位操作按钮组。
@onready var slot_actions: VBoxContainer = $MenuLayer/Menu/Center/Panel/Content/Rows/SlotActions
## 五个槽位按钮的容器。
@onready var slot_list: VBoxContainer = $MenuLayer/Menu/Center/Panel/Content/Rows/SlotActions/Slots
## 操作结果与错误提示。
@onready var message: Label = $MenuLayer/Menu/Center/Panel/Content/Rows/Message


## 绑定各视图并加载玩家附近区块。
func _ready() -> void:
	debug_panel.view_height_changed.connect(_on_view_height_changed)
	debug_panel.set_save_directory(ProjectSettings.globalize_path(save_slots.save_dir))
	_bind_session()
	_on_view_height_changed(debug_panel.view_height_slider.value)
	$MenuLayer/Menu/Center/Panel/Content/Rows/TitleActions/StartButton.pressed.connect(start_new_game)
	$MenuLayer/Menu/Center/Panel/Content/Rows/TitleActions/LoadButton.pressed.connect(show_load_slots)
	$MenuLayer/Menu/Center/Panel/Content/Rows/SlotActions/BackButton.pressed.connect(_on_back_pressed)
	$RadarLayer/GameButtons/SaveButton.pressed.connect(show_save_slots)
	$RadarLayer/GameButtons/TitleButton.pressed.connect(show_title)
	show_title()


## 采集移动输入、推进模拟并同步画面。
func _physics_process(delta: float) -> void:
	if mode != MODE_PLAYING or world_map.visible:
		return
	session.set_player_direction(MovementInput.from_keyboard())
	session.step(delta)
	_sync_view()


## 在 GUI 接收空格前处理地块快捷键，避免聚焦开关被触发。
func _input(event: InputEvent) -> void:
	if mode != MODE_PLAYING or world_map.visible or not event is InputEventKey or not event.pressed or event.echo or event.keycode != KEY_SPACE:
		return
	var focused := get_viewport().gui_get_focus_owner()
	if focused is LineEdit and focused.editable:
		return
	if focused is TextEdit and focused.editable:
		return
	session.world.toggle_tile(session.player_position())
	get_viewport().set_input_as_handled()


## 处理地图和边缘显示快捷键。
func _unhandled_key_input(event: InputEvent) -> void:
	if mode != MODE_PLAYING or not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_M:
		world_map.toggle_open()
	elif world_map.visible:
		return
	elif event.keycode == KEY_F3:
		session.world.set_debug_edges(not session.world.debug().edges)


## 按视野高度倍数缩小镜头缩放。
func _on_view_height_changed(multiplier: float) -> void:
	camera.zoom = Vector2.ONE * (DEFAULT_CAMERA_ZOOM / multiplier)


## 重建玩家显示节点并恢复当前世界状态。
func rebuild_player_view() -> void:
	if is_instance_valid(player_view):
		player_view.queue_free()
	player_view = PLAYER_VIEW.instantiate()
	add_child(player_view)
	_sync_view()


## 从玩家实体状态更新角色位置、朝向与镜头。
func _sync_view() -> void:
	if not is_instance_valid(player_view):
		return
	var position := session.player_position()
	player_view.position = UtilIsometricProjection.to_screen(position)
	player_view.facing = session.player_facing()
	camera.position = player_view.position


## 让全部世界视图订阅当前会话并更新玩家显示节点。
func _bind_session() -> void:
	chat_panel.clear_messages()
	session.world.system_error.connect(_on_system_error)
	ground.bind(session.world)
	radar.bind(session.world, session.player_id)
	debug_panel.bind(session.world)
	world_map.bind(session.world, session.player_id)
	rebuild_player_view()
	_sync_view()


## 将世界运行错误追加为系统消息。
func _on_system_error(text: String) -> void:
	chat_panel.add_system_message(text)


## 创建初始状态的新会话并进入游戏。
func start_new_game() -> void:
	session = LocalSession.new()
	_bind_session()
	_enter_game()


## 显示标题选项并暂停会话推进。
func show_title() -> void:
	mode = MODE_TITLE
	menu.visible = true
	game_buttons.visible = false
	world_map.visible = false
	heading.text = "RPG"
	subtitle.text = "连续世界测试原型"
	title_actions.visible = true
	slot_actions.visible = false
	message.text = ""


## 显示五个可读取的槽位。
func show_load_slots() -> void:
	mode = MODE_LOAD
	menu.visible = true
	game_buttons.visible = false
	heading.text = "读取进度"
	subtitle.text = "选择一个已有存档"
	title_actions.visible = false
	slot_actions.visible = true
	message.text = ""
	_refresh_slots()


## 显示五个可覆盖保存的槽位。
func show_save_slots() -> void:
	mode = MODE_SAVE
	menu.visible = true
	game_buttons.visible = false
	heading.text = "保存进度"
	subtitle.text = "选择槽位保存当前进度"
	title_actions.visible = false
	slot_actions.visible = true
	message.text = ""
	_refresh_slots()


## 为当前菜单重建五个槽位的状态与操作按钮。
func _refresh_slots() -> void:
	for child in slot_list.get_children():
		slot_list.remove_child(child)
		child.queue_free()
	for slot in range(1, SaveSlots.SLOT_COUNT + 1):
		var info := save_slots.inspect(slot)
		var button := Button.new()
		button.custom_minimum_size = Vector2(0, 44)
		match info["state"]:
			"valid":
				button.text = "存档 %d  ·  %s" % [slot, info["saved_at"]]
			"invalid":
				button.text = "存档 %d  ·  无效或不兼容" % slot
			_:
				button.text = "存档 %d  ·  空" % slot
		button.disabled = mode == MODE_LOAD and info["state"] != "valid"
		button.pressed.connect(_on_slot_pressed.bind(slot))
		slot_list.add_child(button)


## 保存选中槽位或从有效槽位创建恢复后的会话。
func _on_slot_pressed(slot: int) -> void:
	if mode == MODE_SAVE:
		if save_slots.save(slot, session):
			message.text = "进度已保存到存档 %d" % slot
			_refresh_slots()
		else:
			message.text = "保存失败，请检查存档目录。"
			chat_panel.add_system_message(message.text)
	elif mode == MODE_LOAD:
		var snapshot := save_slots.read(slot)
		if snapshot.is_empty():
			message.text = "存档无效或不兼容。"
			chat_panel.add_system_message(message.text)
			_refresh_slots()
			return
		var restored := LocalSession.new(snapshot)
		session = restored
		_bind_session()
		_enter_game()


## 从槽位菜单返回先前的标题或游戏画面。
func _on_back_pressed() -> void:
	if mode == MODE_SAVE:
		_enter_game()
	else:
		show_title()


## 关闭菜单并继续模拟当前会话。
func _enter_game() -> void:
	mode = MODE_PLAYING
	menu.visible = false
	game_buttons.visible = true
