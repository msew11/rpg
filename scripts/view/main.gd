extends Node2D

const PLAYER_VIEW := preload("res://scenes/player_view.tscn")

@onready var camera: Camera2D = $Camera2D
@onready var ground: ChunkGround = $ChunkGround
@onready var radar: ChunkRadar = $RadarLayer/ChunkRadar
@onready var debug_panel: DebugPanel = $RadarLayer/DebugPanel

var session := LocalSession.new()
var catalog := TestChunkCatalog.new()
var chunk_world := ChunkWorld.new(session.world, catalog)
var player_view: Node2D


func _ready() -> void:
	session.world.write_value(WorldState.DEBUG_EDGES_KEY, false)
	session.world.write_value(WorldState.DEBUG_RADAR_KEY, true)
	ground.bind(session.world)
	radar.bind(session.world, session.player_id)
	debug_panel.bind(session.world)
	chunk_world.load_around(session.player_position())
	rebuild_player_view()
	_sync_view()


func _physics_process(delta: float) -> void:
	session.set_player_direction(MovementInput.from_keyboard())
	session.step(delta)
	chunk_world.load_around(session.player_position())
	_sync_view()


func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_SPACE:
		chunk_world.toggle_tile(session.player_position())
	elif event.keycode == KEY_F3:
		session.world.write_value(WorldState.DEBUG_EDGES_KEY, not session.world.read_value(WorldState.DEBUG_EDGES_KEY))


func rebuild_player_view() -> void:
	if is_instance_valid(player_view):
		player_view.queue_free()
	player_view = PLAYER_VIEW.instantiate()
	add_child(player_view)
	_sync_view()


func _sync_view() -> void:
	if not is_instance_valid(player_view):
		return
	var position: Vector2 = session.world.read_value(WorldState.position_key(session.player_id))
	player_view.position = IsometricProjection.to_screen(position)
	player_view.facing = session.world.read_value(WorldState.facing_key(session.player_id))
	camera.position = player_view.position
