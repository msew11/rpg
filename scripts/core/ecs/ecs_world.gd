# 管理 ECS 实体、组件和系统的生命周期与调度。
class_name EcsWorld
extends RefCounted

# 分配给下一个实体的标识，保证销毁后不复用。
var _next_entity_id: int = 1
# 记录当前有效的实体标识。
var _entities: Dictionary = {}
# 按实体标识和组件脚本类型保存组件。
var _components: Dictionary = {}
# 保存系统的注册顺序。
var _systems: Array[EcsSystem] = []
# 记录每次系统注册的令牌，用于识别更新期间的重新注册。
var _system_tokens: Dictionary = {}
# 分配给下一次系统注册的令牌。
var _next_system_token: int = 1


# 创建实体并返回不会复用的标识。
func create_entity() -> int:
	var entity_id := _next_entity_id
	_next_entity_id += 1
	_entities[entity_id] = true
	_components[entity_id] = {}
	return entity_id


# 判断实体标识当前是否有效。
func has_entity(entity_id: int) -> bool:
	return _entities.has(entity_id)


# 销毁实体及其组件；标识无效时返回 false。
func destroy_entity(entity_id: int) -> bool:
	if not has_entity(entity_id):
		return false
	_entities.erase(entity_id)
	_components.erase(entity_id)
	return true


# 按脚本类型附加或替换实体组件。
func add_component(entity_id: int, component: EcsComponent) -> bool:
	if not has_entity(entity_id) or component == null:
		return false
	var component_type := component.get_script() as Script
	if component_type == null:
		return false
	var entity_components: Dictionary = _components[entity_id]
	entity_components[component_type] = component
	return true


# 判断有效实体是否具有指定脚本类型的组件。
func has_component(entity_id: int, component_type: Script) -> bool:
	if not has_entity(entity_id) or component_type == null:
		return false
	var entity_components: Dictionary = _components[entity_id]
	return entity_components.has(component_type)


# 取得组件实例；实体或组件不存在时返回 null。
func get_component(entity_id: int, component_type: Script) -> EcsComponent:
	if not has_component(entity_id, component_type):
		return null
	var entity_components: Dictionary = _components[entity_id]
	return entity_components[component_type] as EcsComponent


# 移除指定类型组件，并报告它此前是否存在。
func remove_component(entity_id: int, component_type: Script) -> bool:
	if not has_component(entity_id, component_type):
		return false
	var entity_components: Dictionary = _components[entity_id]
	entity_components.erase(component_type)
	return true


# 返回具有全部指定组件的实体标识快照，按创建顺序排列。
func query(component_types: Array[Script] = []) -> Array[int]:
	var matches: Array[int] = []
	for entity_id: int in _entities:
		var contains_all := true
		for component_type: Script in component_types:
			if not has_component(entity_id, component_type):
				contains_all = false
				break
		if contains_all:
			matches.append(entity_id)
	matches.sort()
	return matches


# 注册系统；同一实例不能重复注册。
func add_system(system: EcsSystem) -> bool:
	if system == null or _systems.has(system):
		return false
	_systems.append(system)
	_system_tokens[system] = _next_system_token
	_next_system_token += 1
	return true


# 注销系统，并报告它此前是否已注册。
func remove_system(system: EcsSystem) -> bool:
	var index := _systems.find(system)
	if index == -1:
		return false
	_systems.remove_at(index)
	_system_tokens.erase(system)
	return true


# 按注册顺序更新系统；执行期间新增或重新注册的系统留到下次更新。
func step(delta: float) -> void:
	var scheduled: Array[Array] = []
	for system in _systems:
		scheduled.append([system, _system_tokens[system]])
	for entry in scheduled:
		var system: EcsSystem = entry[0]
		if _system_tokens.get(system) == entry[1]:
			system.update(self, delta)
