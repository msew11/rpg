# 定义由 ECS 世界调度的系统基类。
@abstract
class_name EcsSystem
extends RefCounted

# 处理一次世界更新，接收当前世界和时间增量。
@abstract func update(world: EcsWorld, delta: float) -> void
