# ECS 核心

`scripts/core/ecs/` 提供与玩法无关的内存 ECS。调用方创建 `EcsWorld`，用整数 ID 表示实体；组件继承 `EcsComponent`，系统继承 `EcsSystem`。运行时不依赖场景节点，因此由调用方决定何时调用 `step(delta)`。

```gdscript
class Position extends EcsComponent:
	var x: float = 0.0

class MoveSystem extends EcsSystem:
	func update(world: EcsWorld, delta: float) -> void:
		for entity_id in world.query([Position]):
			var position := world.get_component(entity_id, Position) as Position
			position.x += delta

func run_example() -> void:
	var world := EcsWorld.new()
	var entity_id := world.create_entity()
	world.add_component(entity_id, Position.new())
	world.add_system(MoveSystem.new())
	world.step(1.0 / 60.0)
	world.destroy_entity(entity_id)
```

| 操作 | 行为 |
| --- | --- |
| `create_entity()`, `destroy_entity(id)`, `has_entity(id)` | 创建、销毁和检查实体。ID 单调递增，销毁后不复用；无效 ID 的销毁返回 `false`。 |
| `add_component(id, component)` | 按组件实例的实际脚本类型附加；同类型再次附加会替换旧实例。无效 ID 返回 `false`。 |
| `has_component(id, Type)`, `get_component(id, Type)`, `remove_component(id, Type)` | 按精确脚本类型操作；缺失时分别返回 `false`、`null`、`false`。 |
| `query([TypeA, TypeB])` | 返回同时具有指定类型的实体 ID 快照，按创建顺序排列。`query()` 返回全部有效实体。 |
| `add_system(system)`, `remove_system(system)`, `step(delta)` | 系统按注册顺序每步执行一次。重复注册同一实例返回 `false`；更新中新增的系统下一步执行，已移除的系统立即跳过。 |

组件是可变引用；本版不跟踪字段变化。调用方应避免让系统长期持有世界的强引用，以免与世界持有的系统形成 `RefCounted` 引用环。执行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 会运行 `tests/test_ecs.gd` 中的 ECS 验收测试。
