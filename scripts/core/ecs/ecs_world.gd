class_name EcsWorld
extends RefCounted

var _next_entity_id: int = 1
var _entities: Dictionary = {}
var _components: Dictionary = {}
var _systems: Array[EcsSystem] = []
var _system_tokens: Dictionary = {}
var _next_system_token: int = 1


func create_entity() -> int:
	var entity_id := _next_entity_id
	_next_entity_id += 1
	_entities[entity_id] = true
	_components[entity_id] = {}
	return entity_id


func has_entity(entity_id: int) -> bool:
	return _entities.has(entity_id)


func destroy_entity(entity_id: int) -> bool:
	if not has_entity(entity_id):
		return false
	_entities.erase(entity_id)
	_components.erase(entity_id)
	return true


func add_component(entity_id: int, component: EcsComponent) -> bool:
	if not has_entity(entity_id) or component == null:
		return false
	var component_type := component.get_script() as Script
	if component_type == null:
		return false
	var entity_components: Dictionary = _components[entity_id]
	entity_components[component_type] = component
	return true


func has_component(entity_id: int, component_type: Script) -> bool:
	if not has_entity(entity_id) or component_type == null:
		return false
	var entity_components: Dictionary = _components[entity_id]
	return entity_components.has(component_type)


func get_component(entity_id: int, component_type: Script) -> EcsComponent:
	if not has_component(entity_id, component_type):
		return null
	var entity_components: Dictionary = _components[entity_id]
	return entity_components[component_type] as EcsComponent


func remove_component(entity_id: int, component_type: Script) -> bool:
	if not has_component(entity_id, component_type):
		return false
	var entity_components: Dictionary = _components[entity_id]
	entity_components.erase(component_type)
	return true


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


func add_system(system: EcsSystem) -> bool:
	if system == null or _systems.has(system):
		return false
	_systems.append(system)
	_system_tokens[system] = _next_system_token
	_next_system_token += 1
	return true


func remove_system(system: EcsSystem) -> bool:
	var index := _systems.find(system)
	if index == -1:
		return false
	_systems.remove_at(index)
	_system_tokens.erase(system)
	return true


func step(delta: float) -> void:
	var scheduled: Array[Array] = []
	for system in _systems:
		scheduled.append([system, _system_tokens[system]])
	for entry in scheduled:
		var system: EcsSystem = entry[0]
		if _system_tokens.get(system) == entry[1]:
			system.update(self, delta)
