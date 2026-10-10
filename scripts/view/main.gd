extends Node2D

const PLAYER_VIEW := preload("res://scenes/player_view.tscn")

@onready var camera: Camera2D = $Camera2D

var session := LocalSession.new()
var player_view: Node2D


func _ready() -> void:
	rebuild_player_view()
	_sync_view()


func _physics_process(delta: float) -> void:
	session.set_player_direction(MovementInput.from_keyboard())
	session.step(delta)
	_sync_view()


func rebuild_player_view() -> void:
	if is_instance_valid(player_view):
		player_view.queue_free()
	player_view = PLAYER_VIEW.instantiate()
	add_child(player_view)
	_sync_view()


func _sync_view() -> void:
	if not is_instance_valid(player_view):
		return
	player_view.position = IsometricProjection.to_screen(session.player_position())
	player_view.facing = session.player_facing()
	camera.position = player_view.position
