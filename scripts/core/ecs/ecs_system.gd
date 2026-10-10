@abstract
class_name EcsSystem
extends RefCounted

## Called once for each EcsWorld.step while this system is registered.
@abstract func update(world: EcsWorld, delta: float) -> void
