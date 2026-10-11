# 验证 openspec/specs/ecs-runtime/spec.md 中的创建和销毁、无效标识、附加与替换、移除组件场景。
# 同时验证多类型匹配、查询快照、顺序与增量、更新期间变更系统集合场景。
class_name TestEcsContract
extends SceneTree

class Position extends EcsComponent:
	var x: float

	func _init(value: float = 0.0) -> void:
		x = value


class Velocity extends EcsComponent:
	var x: float = 1.0


class RecordingSystem extends EcsSystem:
	var name_tag: String
	var calls: Array[String]
	var expected_world_id: int
	var expected_delta: float
	var world_to_change: EcsWorld
	var system_to_add: EcsSystem
	var system_to_remove: EcsSystem

	func _init(tag: String, log: Array[String]) -> void:
		name_tag = tag
		calls = log

	func update(world: EcsWorld, delta: float) -> void:
		calls.append(name_tag)
		assert(world.get_instance_id() == expected_world_id)
		assert(delta == expected_delta)
		if world_to_change != null:
			world_to_change.remove_system(system_to_remove)
			world_to_change.add_system(system_to_add)
			world_to_change = null


var _failures: Array[String] = []


# 执行 ECS 契约的验收场景，并以退出码报告结果。
func _initialize() -> void:
	var world := EcsWorld.new()
	var first := world.create_entity()
	var second := world.create_entity()
	var third := world.create_entity()
	_check(first != second and second != third, "entity IDs are unique")
	_check(world.has_entity(first), "created entity exists")
	_check(not world.has_entity(999), "unknown entity is absent")
	_check(not world.add_component(999, Position.new()), "unknown entity rejects component")
	_check(not world.has_entity(999), "component operation does not create entity")
	_check(world.get_component(first, Position) == null, "missing component reads null")

	var old_position := Position.new(1.0)
	var new_position := Position.new(2.0)
	_check(world.add_component(first, old_position), "add position")
	_check(world.add_component(first, new_position), "replace position")
	_check(world.get_component(first, Position) == new_position, "replacement is current component")
	_check(world.has_component(first, Position), "has component")
	_check(world.add_component(first, Velocity.new()), "add velocity")
	_check(world.add_component(second, Position.new()), "add second position")
	_check(world.query([Position, Velocity]) == [first], "multi-type query matches all")
	_check(world.query([Position]) == [first, second], "query preserves creation order")
	var snapshot := world.query()
	_check(snapshot == [first, second, third], "empty query lists all entities")
	_check(world.remove_component(first, Velocity), "remove existing component")
	_check(not world.remove_component(first, Velocity), "removing absent component reports no change")
	_check(not world.has_component(first, Velocity), "removed component is absent")
	_check(world.query([Position, Velocity]).is_empty(), "query reflects component removal")
	_check(world.destroy_entity(first), "destroy existing entity")
	_check(not world.destroy_entity(first), "destroying twice reports no change")
	_check(not world.has_entity(first), "destroyed entity is absent")
	_check(world.get_component(first, Position) == null, "destroy discards components")
	_check(not world.add_component(first, Position.new()), "destroyed entity rejects components")
	_check(snapshot == [first, second, third], "query result is a snapshot")
	_check(world.query() == [second, third], "new query excludes destroyed entity")
	_check(world.create_entity() > third, "destroyed IDs are not reused")

	var log: Array[String] = []
	var alpha := RecordingSystem.new("alpha", log)
	var beta := RecordingSystem.new("beta", log)
	var gamma := RecordingSystem.new("gamma", log)
	for system in [alpha, beta, gamma]:
		system.expected_world_id = world.get_instance_id()
		system.expected_delta = 0.25
	alpha.world_to_change = world
	alpha.system_to_add = gamma
	alpha.system_to_remove = beta
	_check(world.add_system(alpha), "register first system")
	_check(world.add_system(beta), "register second system")
	_check(not world.add_system(alpha), "duplicate system rejected")
	world.step(0.25)
	_check(log == ["alpha"], "removed system skipped and new system deferred")
	world.step(0.25)
	_check(log == ["alpha", "alpha", "gamma"], "systems follow registration order")
	_check(not world.remove_system(beta), "removed system is no longer registered")
	_check(world.remove_system(alpha), "remove registered system")
	world.step(0.25)
	_check(log == ["alpha", "alpha", "gamma", "gamma"], "removed system stays inactive")
	var replacement_world := EcsWorld.new()
	var replacement_log: Array[String] = []
	var changer := RecordingSystem.new("changer", replacement_log)
	var readded := RecordingSystem.new("readded", replacement_log)
	for system in [changer, readded]:
		system.expected_world_id = replacement_world.get_instance_id()
		system.expected_delta = 0.25
	changer.world_to_change = replacement_world
	changer.system_to_remove = readded
	changer.system_to_add = readded
	replacement_world.add_system(changer)
	replacement_world.add_system(readded)
	replacement_world.step(0.25)
	_check(replacement_log == ["changer"], "remove and re-add waits until next step")
	replacement_world.step(0.25)
	_check(replacement_log == ["changer", "changer", "readded"], "re-added system follows new registration order")

	if _failures.is_empty():
		print("ECS contract passed.")
		quit(0)
	else:
		for failure in _failures:
			printerr(failure)
		quit(1)


# 收集未满足的契约断言，供检查脚本报告。
func _check(condition: bool, description: String) -> void:
	if not condition:
		_failures.append(description)
