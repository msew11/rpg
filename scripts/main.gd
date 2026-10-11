## 协调本地会话、区块、输入、玩家视图和镜头。
class_name Main
extends Node2D

## 用于重建玩家显示节点的场景。
const PLAYER_VIEW := preload("res://scenes/player_view.tscn")

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

## 接收输入并推进世界状态的本地会话。
var session := LocalSession.new()
## 当前玩家显示节点。
var player_view: Node2D


## 绑定各视图并加载玩家附近区块。
func _ready() -> void:
	ground.bind(session.world)
	radar.bind(session.world, session.player_id)
	debug_panel.bind(session.world)
	world_map.bind(session.world, session.player_id)
	rebuild_player_view()
	_sync_view()


## 采集移动输入、推进模拟并同步画面。
func _physics_process(delta: float) -> void:
	if world_map.visible:
		return
	session.set_player_direction(MovementInput.from_keyboard())
	session.step(delta)
	_sync_view()


## 处理地图、地块切换和边缘显示快捷键。
func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_M:
		world_map.toggle_open()
	elif world_map.visible:
		return
	elif event.keycode == KEY_SPACE:
		session.world.toggle_tile(session.player_position())
	elif event.keycode == KEY_F3:
		session.world.set_debug_edges(not session.world.debug().edges)


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
