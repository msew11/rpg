## 绘制玩家占位形象或八方向像素素体。
class_name PlayerView
extends Node2D

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
## 16×16 素体的待机图。
const SMALL_IDLE := preload("res://assets/player_templates/erisesra/16x16/idle.png")
## 16×16 素体的行走图。
const SMALL_WALK := preload("res://assets/player_templates/erisesra/16x16/walk.png")
## 16×32 素体的待机图。
const TALL_IDLE := preload("res://assets/player_templates/erisesra/16x32/idle.png")
## 16×32 素体的行走图。
const TALL_WALK := preload("res://assets/player_templates/erisesra/16x32/walk.png")

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


## 按固定间隔循环播放四帧动画。
func _process(delta: float) -> void:
	if template_id == TEMPLATE_PLACEHOLDER:
		return
	frame_elapsed += delta
	if frame_elapsed >= FRAME_SECONDS:
		frame_index = (frame_index + int(frame_elapsed / FRAME_SECONDS)) % 4
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
	sprite.texture = (SMALL_WALK if moving else SMALL_IDLE) if template_id == TEMPLATE_SMALL else (TALL_WALK if moving else TALL_IDLE)
	sprite.region_rect = Rect2(frame_index * frame_size, direction_row() * frame_size, frame_size, frame_size)
	sprite.flip_h = facing.x < 0
	sprite.scale = Vector2.ONE * display_scale
	sprite.position = Vector2(-frame_size / 2.0, -frame_size) * display_scale


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
