## 绘制玩家占位形象或八方向像素素体。
class_name PlayerView
extends Node2D

## 角色移动状态切换导致演示停止时通知面板。
signal demo_stopped

## 原有占位角色的模板编号。
const TEMPLATE_PLACEHOLDER := 0
## 16×16 素体的模板编号。
const TEMPLATE_SMALL := 1
## 16×32 素体的模板编号。
const TEMPLATE_TALL := 2
## 16×16 素体的帧边长。
const SMALL_FRAME := 24
## 16×16 素体相对当前地块的整数显示倍率。
const SMALL_DISPLAY_SCALE := 3.0
## 16×32 素体的帧边长。
const TALL_FRAME := 32
## 动画每帧持续的秒数。
const FRAME_SECONDS := 0.16
## 跟随角色移动状态的默认动作。
const ACTION_DEFAULT := 0
## 待机演示编号。
const ACTION_IDLE := 1
## 行走演示编号。
const ACTION_WALK := 2
## 奔跑演示编号。
const ACTION_RUN := 3
## 攻击演示编号。
const ACTION_ATTACK := 4
## 交互演示编号。
const ACTION_INTERACT := 5
## 跳跃演示编号。
const ACTION_JUMP := 6
## 转身演示编号。
const ACTION_ROTATE := 7
## 16×16 素体的待机图。
const SMALL_IDLE := preload("res://assets/player_templates/erisesra/16x16/idle.png")
## 16×16 素体的行走图。
const SMALL_WALK := preload("res://assets/player_templates/erisesra/16x16/walk.png")
## 16×16 素体的动作素材。
const SMALL_RUN := preload("res://assets/player_templates/erisesra/16x16/run.png")
## 16×16 素体的攻击素材。
const SMALL_ATTACK := preload("res://assets/player_templates/erisesra/16x16/attack.png")
## 16×16 素体的交互素材。
const SMALL_INTERACT := preload("res://assets/player_templates/erisesra/16x16/interact.png")
## 16×16 素体的跳跃素材。
const SMALL_JUMP := preload("res://assets/player_templates/erisesra/16x16/jump.png")
## 16×16 素体的转身素材。
const SMALL_ROTATE := preload("res://assets/player_templates/erisesra/16x16/rotate.png")
## 16×32 素体的待机图。
const TALL_IDLE := preload("res://assets/player_templates/erisesra/16x32/idle.png")
## 16×32 素体的行走图。
const TALL_WALK := preload("res://assets/player_templates/erisesra/16x32/walk.png")
## 16×32 素体的奔跑素材。
const TALL_RUN := preload("res://assets/player_templates/erisesra/16x32/run.png")
## 16×32 素体的攻击素材。
const TALL_ATTACK := preload("res://assets/player_templates/erisesra/16x32/attack.png")
## 16×32 素体的交互素材。
const TALL_INTERACT := preload("res://assets/player_templates/erisesra/16x32/interact.png")
## 16×32 素体的跳跃素材。
const TALL_JUMP := preload("res://assets/player_templates/erisesra/16x32/jump.png")
## 16×32 素体的转身素材。
const TALL_ROTATE := preload("res://assets/player_templates/erisesra/16x32/rotate.png")

## 裁剪精灵表的显示节点。
@onready var sprite: Sprite2D = $Sprite2D

## 当前使用的显示模板。
var template_id := TEMPLATE_TALL:
	set(value):
		template_id = value
		_update_sprite()
		queue_redraw()

## 玩家当前显示的八方向朝向。
var facing := Vector2i(0, 1):
	set(value):
		facing = value
		_update_sprite()
		queue_redraw()

## 当前是否播放行走动作。
var moving := false:
	set(value):
		if moving == value:
			return
		moving = value
		if demo_action != ACTION_DEFAULT:
			demo_action = ACTION_DEFAULT
			demo_stopped.emit()
		frame_index = 0
		frame_elapsed = 0.0
		_update_sprite()

## GM 面板指定的展示动作。
var demo_action := ACTION_DEFAULT:
	set(value):
		if demo_action == value:
			return
		demo_action = value
		frame_index = 0
		frame_elapsed = 0.0
		_update_sprite()

## 当前动画帧序号。
var frame_index := 0
## 当前帧已经播放的秒数。
var frame_elapsed := 0.0


## 初始化像素采样并显示默认模板。
func _ready() -> void:
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	sprite.centered = false
	sprite.region_enabled = true
	_update_sprite()


## 按素材的实际帧数循环播放动作。
func _process(delta: float) -> void:
	if template_id == TEMPLATE_PLACEHOLDER:
		return
	frame_elapsed += delta
	if frame_elapsed >= FRAME_SECONDS:
		var frame_size := SMALL_FRAME if template_id == TEMPLATE_SMALL else TALL_FRAME
		var frame_count := maxi(1, int(_action_texture().get_width() / frame_size))
		frame_index = (frame_index + int(frame_elapsed / FRAME_SECONDS)) % frame_count
		frame_elapsed = fmod(frame_elapsed, FRAME_SECONDS)
		_update_sprite()


## 选择精灵表、方向行和脚部对齐的帧区域。
func _update_sprite() -> void:
	if not is_node_ready():
		return
	sprite.visible = template_id != TEMPLATE_PLACEHOLDER
	if not sprite.visible:
		return
	var frame_size := SMALL_FRAME if template_id == TEMPLATE_SMALL else TALL_FRAME
	var display_scale := SMALL_DISPLAY_SCALE if template_id == TEMPLATE_SMALL else 1.0
	sprite.texture = _action_texture()
	var row_height := 22 if template_id == TEMPLATE_SMALL and demo_action == ACTION_JUMP else frame_size
	var row := 0 if demo_action == ACTION_ROTATE else direction_row()
	sprite.region_rect = Rect2(frame_index * frame_size, row * row_height, frame_size, row_height)
	sprite.flip_h = facing.x < 0 and demo_action != ACTION_ROTATE
	sprite.scale = Vector2.ONE * display_scale
	sprite.position = Vector2(-frame_size / 2.0, -row_height) * display_scale


## 返回当前模板和演示动作对应的素材。
func _action_texture() -> Texture2D:
	var action := demo_action
	if action == ACTION_DEFAULT:
		action = ACTION_WALK if moving else ACTION_IDLE
	if template_id == TEMPLATE_SMALL:
		match action:
			ACTION_WALK: return SMALL_WALK
			ACTION_RUN: return SMALL_RUN
			ACTION_ATTACK: return SMALL_ATTACK
			ACTION_INTERACT: return SMALL_INTERACT
			ACTION_JUMP: return SMALL_JUMP
			ACTION_ROTATE: return SMALL_ROTATE
			_: return SMALL_IDLE
	match action:
		ACTION_WALK: return TALL_WALK
		ACTION_RUN: return TALL_RUN
		ACTION_ATTACK: return TALL_ATTACK
		ACTION_INTERACT: return TALL_INTERACT
		ACTION_JUMP: return TALL_JUMP
		ACTION_ROTATE: return TALL_ROTATE
		_: return TALL_IDLE


## 将八方向映射到素材的下、右下、右、右上、上五行。
func direction_row() -> int:
	if facing.y > 0:
		return 0 if facing.x == 0 else 1
	if facing.y < 0:
		return 4 if facing.x == 0 else 3
	return 2


## 绘制以脚部为锚点的占位角色和朝向箭头。
func _draw() -> void:
	if template_id != TEMPLATE_PLACEHOLDER:
		return
	# 脚部锚点为原点，占位形象用箭头区分八个朝向。
	draw_ellipse_shadow()
	draw_rect(Rect2(-9, -29, 18, 25), Color("26374f"))
	draw_circle(Vector2(0, -30), 10, Color("eac091"))
	var direction := facing_screen_direction()
	var arrow_start := Vector2(0, -19)
	var arrow_end := arrow_start + direction * 23.0
	draw_line(arrow_start, arrow_end, Color("fff3a6"), 4.0, true)
	var side := direction.orthogonal() * 5.0
	draw_colored_polygon(PackedVector2Array([
		arrow_end + direction * 6.0,
		arrow_end - direction * 4.0 + side,
		arrow_end - direction * 4.0 - side
	]), Color("fff3a6"))


## 将逻辑朝向转换为屏幕上的箭头方向。
func facing_screen_direction() -> Vector2:
	return UtilIsometricProjection.to_screen(UtilIsometricProjection.movement_direction(facing)).normalized()


## 在脚部绘制压扁的角色阴影。
func draw_ellipse_shadow() -> void:
	draw_set_transform(Vector2.ZERO, 0.0, Vector2(1.0, 0.38))
	draw_circle(Vector2(0, 0), 14, Color(0.08, 0.12, 0.11, 0.45))
	draw_set_transform(Vector2.ZERO)
